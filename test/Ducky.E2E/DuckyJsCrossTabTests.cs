using Microsoft.Playwright;

namespace Ducky.E2E;

// The registration, relay and delivery-failure handling of ducky.js (SPEC §11.7, §11.9, INV-18) on the static harness
// page. The clock is paused, so every module timer fires only when a spec runs it (§17.2). Each fake DotNetObjectReference
// logs "<ref>@<ms since t0>: <OnStorageChanged arguments>" into window.calls and answers as its mode says: "ok" resolves,
// "down" rejects with a connection error, "gone" with the untracked-object error (WebAssembly's, or Server's with
// DetailedErrors), "hold" keeps the promise in pending, "slow" resolves after ref.latency ms.
public sealed class DuckyJsCrossTabTests
{
    // 1 s doubling to the 30 s cap (§11.7), from the first failure at 0.
    private static readonly int[] _retries = [1_000, 3_000, 7_000, 15_000, 31_000, 61_000, 91_000];

    // The same steps when every delivery hangs: each retry comes 1 s later, once the previous one counts as failed.
    private static readonly int[] _hungRetries = [2_000, 5_000, 10_000, 19_000, 36_000, 67_000];

    private const string Prelude = """
        async () => {
            window.ducky = await import('/ducky.js');
            window.calls = [];
            window.t0 = Date.now();
            window.makeRef = name => {
                const ref = {
                    mode: 'ok',
                    pending: [],
                    invokeMethodAsync(method, ...args) {
                        calls.push(`${name}@${Date.now() - t0}: ${method} ${args.map(String).join(' ')}`);
                        if (ref.mode === 'hold') return new Promise((resolve, reject) => ref.pending.push({ resolve, reject }));
                        if (ref.mode === 'slow') return new Promise(resolve => setTimeout(resolve, ref.latency));
                        if (ref.mode === 'down') return Promise.reject(new Error('Cannot send data if the connection is not in the \'Connected\' State.'));
                        if (ref.mode === 'gone') return Promise.reject(new Error("System.ArgumentException: There is no tracked object with id '3'. Perhaps the DotNetObjectReference instance was already disposed."));
                        return Promise.resolve();
                    },
                };
                return ref;
            };
            window.fire = (key, newValue, storageArea) =>
                dispatchEvent(new StorageEvent('storage', { key, newValue, storageArea: storageArea ?? localStorage }));
            window.take = () => calls.splice(0);
        }
        """;

    [Fact]
    public async Task DuckyJs_Relay_SkipsWriterId()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var relayed = await page.EvaluateAsync<string[]>("""
            () => {
                ducky.watchStorage('a', makeRef('a'), 'app', false, 600000, 16);
                ducky.watchStorage('b', makeRef('b'), 'app', false, 600000, 16);
                ducky.watchStorage('b', makeRef('b2'), 'app', true, 600000, 16);
                ducky.watchStorage('c', makeRef('c'), 'other', false, 600000, 16);
                const out = [];
                const step = (label, result) => out.push(`${label} -> ${result}`, ...take());
                step('set local by a', ducky.storageSet('local', 'app:k', '{"v":1}', 'a'));
                step('set session by b, over a\'s budget', ducky.storageSet('session', 'app:k', '語語語語語', 'b'));
                step('remove session by b', ducky.storageRemove('session', 'app:k', 'b'));
                step('set by an unregistered writer', ducky.storageSet('local', 'app:k', '{}', 'gone'));
                step('remove by an unregistered writer', ducky.storageRemove('local', 'app:k', 'gone'));
                step('set another prefix', ducky.storageSet('local', 'apple:k', '{}', 'a'));
                step('set by c', ducky.storageSet('local', 'other:k', '{}', 'c'));
                fire('app:k', '{}');
                step('a storage event for ids registered without crossTab', true);
                ducky.unwatchStorage('b');
                step('set after b unwatched', ducky.storageSet('local', 'app:k', '{}', 'a'));
                return out;
            }
            """);

        relayed.ShouldBe([
            "set local by a -> true",
            """b@0: OnStorageChanged doc local app:k {"v":1} false""",
            "set session by b, over a's budget -> true",
            "a@0: OnStorageChanged doc session app:k null true",
            "remove session by b -> true",
            "a@0: OnStorageChanged doc session app:k null false",
            "set by an unregistered writer -> false",
            "a@0: OnStorageChanged doc local app:k {} false",
            "b@0: OnStorageChanged doc local app:k {} false",
            "remove by an unregistered writer -> false",
            "a@0: OnStorageChanged doc local app:k null false",
            "b@0: OnStorageChanged doc local app:k null false",
            "set another prefix -> true",
            "set by c -> true",
            "a storage event for ids registered without crossTab -> true",
            "set after b unwatched -> true",
        ]);
    }

    // The budget (the sixth argument, BlazorOptions.InlinePayloadBytes) caps the UTF-8 length of the JSON-encoded value
    // (§11.9): JSON.stringify adds 2 quotes to a plain string, and each 語 is 1 UTF-16 unit but 3 UTF-8 bytes. A removal has
    // no value to budget, even at 0. A missing or malformed budget (a caller still on the five-argument watchStorage)
    // throws a TypeError and registers nothing, rather than silently sending every value by stream.
    [Fact]
    public async Task DuckyJs_Relay_BudgetIsUtf8BytesOfTheJsonEncoding()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var relayed = await page.EvaluateAsync<string[]>("""
            () => {
                ducky.watchStorage('a', makeRef('a'), 'app', false, 600000, 16);
                ducky.storageSet('session', 'app:k', 'x'.repeat(14), 'w');
                ducky.storageSet('session', 'app:k', 'x'.repeat(15), 'w');
                ducky.storageSet('session', 'app:k', '語語語語語', 'w');
                ducky.unwatchStorage('a');
                ducky.watchStorage('z', makeRef('z'), 'app', false, 600000, 0);
                ducky.storageRemove('session', 'app:k', 'w');
                ducky.unwatchStorage('z');
                const attempts = [[], [-1], [1.5], ['16']].map(budget => {
                    try {
                        ducky.watchStorage('u', makeRef('u'), 'app', false, 600000, ...budget);
                        return 'registered';
                    } catch (e) {
                        return `${e.constructor.name}: ${e.message}`;
                    }
                });
                const registered = ducky.storageSet('session', 'app:k', '{}', 'u');
                return [...take(), ...attempts, `u registered: ${registered}`];
            }
            """);

        relayed.ShouldBe([
            "a@0: OnStorageChanged doc session app:k xxxxxxxxxxxxxx false",
            "a@0: OnStorageChanged doc session app:k null true",
            "a@0: OnStorageChanged doc session app:k null true",
            "z@0: OnStorageChanged doc session app:k null false",
            "TypeError: watchStorage: maxInlineBytes",
            "TypeError: watchStorage: maxInlineBytes",
            "TypeError: watchStorage: maxInlineBytes",
            "TypeError: watchStorage: maxInlineBytes",
            "u registered: false",
        ]);
    }

    [Fact]
    public async Task DuckyJs_StorageListener_ForwardsPrefixedLocalKeysOfCrossTabIds()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var forwarded = await page.EvaluateAsync<string[]>("""
            () => {
                ducky.watchStorage('t', makeRef('t'), 'app', true, 600000, 16);
                ducky.watchStorage('t', makeRef('t2'), 'app', true, 600000, 16);
                ducky.watchStorage('n', makeRef('n'), 'app', false, 600000, 16);
                const out = [];
                const step = (label, key, value, area) => { fire(key, value, area); out.push(label, ...take()); };
                step('value', 'app:k', '{"v":1}');
                step('value over the budget in UTF-8 only', 'app:k', '語語語語語');
                step('removal', 'app:k', null);
                step('clear', null, null);
                step('sessionStorage', 'app:k', '{}', sessionStorage);
                step('another prefix', 'apple:k', '{}');
                ducky.unwatchStorage('t');
                step('after unwatch', 'app:k', '{}');
                return out;
            }
            """);

        forwarded.ShouldBe([
            "value",
            """t@0: OnStorageChanged tab local app:k {"v":1} false""",
            "value over the budget in UTF-8 only",
            "t@0: OnStorageChanged tab local app:k null true",
            "removal",
            "t@0: OnStorageChanged tab local app:k null false",
            "clear",
            "t@0: OnStorageChanged tab local null null false",
            "sessionStorage",
            "another prefix",
            "after unwatch",
        ]);
    }

    // Three (source, area) pairs fail in one disconnect; retries back off 1, 2, 4, 8, 16 s, then every 30 s, and the first
    // success re-reads each pair once. A later failure starts again at 1 s, and a fresh event that gets through first
    // re-reads at once. An id whose deliveries keep failing is pruned at the first failure past pruneAfterMs.
    [Fact]
    public async Task DuckyJs_ResyncBackoff_CapsAt30s_ThenReReadsAll()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'down';
                ducky.watchStorage('a', a, 'app', true, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{"v":1}', 'w');
                ducky.storageSet('session', 'app:k', '{"v":2}', 'w');
                fire('app:k', '{"v":3}');
                ducky.storageRemove('local', 'app:k', 'w');
            }
            """);
        (await Take(page)).ShouldBe([
            """a@0: OnStorageChanged doc local app:k {"v":1} false""",
            """a@0: OnStorageChanged doc session app:k {"v":2} false""",
            """a@0: OnStorageChanged tab local app:k {"v":3} false""",
            "a@0: OnStorageChanged doc local app:k null false",
        ]);

        await page.Clock.RunForAsync(91_000);
        string[] Resync(int at) =>
        [
            $"a@{at}: OnStorageChanged doc local null null false",
            $"a@{at}: OnStorageChanged doc session null null false",
            $"a@{at}: OnStorageChanged tab local null null false",
        ];
        (await Take(page)).ShouldBe(_retries.SelectMany(Resync).ToArray());

        await page.EvaluateAsync("() => { a.mode = 'ok'; }");
        await page.Clock.RunForAsync(29_999);
        (await Take(page)).ShouldBeEmpty();
        await page.Clock.RunForAsync(1);
        (await Take(page)).ShouldBe(Resync(121_000));
        await page.Clock.RunForAsync(120_000);
        (await Take(page)).ShouldBeEmpty();

        await page.EvaluateAsync("() => { a.mode = 'down'; fire('app:k', '{\"v\":4}'); }");
        await page.Clock.RunForAsync(1_000);
        (await Take(page)).ShouldBe([
            """a@241000: OnStorageChanged tab local app:k {"v":4} false""",
            "a@242000: OnStorageChanged tab local null null false",
        ]);
        await page.EvaluateAsync("() => { a.mode = 'ok'; ducky.storageSet('session', 'app:j', '{}', 'w'); }");
        (await Take(page)).ShouldBe([
            "a@242000: OnStorageChanged doc session app:j {} false",
            "a@242000: OnStorageChanged tab local null null false",
        ]);
        await page.Clock.RunForAsync(120_000);
        (await Take(page)).ShouldBeEmpty();

        // Pruning at the cap: failures at 0, 1, 3, 7, 15, 31 s and then every 30 s; 601 s is the first past 600 s.
        await page.EvaluateAsync("""
            () => {
                window.t0 = Date.now();
                window.p = makeRef('p');
                p.mode = 'down';
                ducky.watchStorage('p', p, 'app', true, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'a');
            }
            """);
        await page.Clock.RunForAsync(571_000);
        var beforeCap = await Take(page);
        beforeCap.Length.ShouldBe(24);
        beforeCap[^1].ShouldBe("p@571000: OnStorageChanged doc local null null false");
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:k', '{}', 'p')")).ShouldBeTrue();
        (await Take(page)).ShouldBe(["a@571000: OnStorageChanged doc local app:k {} false"]);

        await page.Clock.RunForAsync(30_000);
        (await Take(page)).ShouldBe(["p@601000: OnStorageChanged doc local null null false"]);
        var pruned = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [`set by p -> ${ducky.storageSet('local', 'app:k', '{}', 'p')}`];
                fire('app:k', '{}');
                return [...out, ...take()];
            }
            """);
        pruned.ShouldBe([
            "set by p -> false",
            "a@601000: OnStorageChanged doc local app:k {} false",
            "a@601000: OnStorageChanged tab local app:k {} false",
        ]);
        await page.Clock.RunForAsync(120_000);
        (await Take(page)).ShouldBeEmpty();
    }

    // A circuit that stayed disconnected past the cap and then reconnected: its writes answer false until .NET re-issues
    // watchStorage, which registers the id afresh, and so does devtoolsSend, the prune having dropped the id's DevTools
    // connection too (§11.7).
    [Fact]
    public async Task DuckyJs_PrunedIdReRegisteredOnNextWrite()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'down';
                ducky.watchStorage('a', a, 'app', false, 2000, 1000);
                window.__REDUX_DEVTOOLS_EXTENSION__ = {
                    connect: () => ({ subscribe() {}, unsubscribe: () => calls.push('unsubscribe'), send: () => calls.push('send') }),
                };
                ducky.devtoolsConnect('a', makeRef('d'), '{"maxInlineBytes":1000}', 600000);
                ducky.storageSet('local', 'app:k', '{"v":1}', 'w');
            }
            """);
        await page.Clock.RunForAsync(3_000);
        var pruned = await Take(page);
        pruned.Length.ShouldBe(4);
        pruned[^1].ShouldBe("unsubscribe");

        var repaired = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [];
                const step = (label, result) => out.push(`${label} -> ${result}`, ...take());
                a.mode = 'ok';
                step('set while pruned', ducky.storageSet('local', 'app:k', '{"v":2}', 'a'));
                step('stored anyway', localStorage.getItem('app:k'));
                step('remove while pruned', ducky.storageRemove('session', 'app:k', 'a'));
                step('relay to the pruned id', ducky.storageSet('local', 'app:k', '{"v":3}', 'w'));
                step('devtools send while pruned', ducky.devtoolsSend('a', '{}', '{}'));
                ducky.watchStorage('a', makeRef('a2'), 'app', false, 2000, 1000);
                step('set after re-registration', ducky.storageSet('local', 'app:k', '{"v":4}', 'a'));
                step('relay after re-registration', ducky.storageSet('local', 'app:k', '{"v":5}', 'w'));
                return out;
            }
            """);

        repaired.ShouldBe([
            "set while pruned -> false",
            """stored anyway -> {"v":2}""",
            "remove while pruned -> false",
            "relay to the pruned id -> false",
            "devtools send while pruned -> false",
            "set after re-registration -> true",
            "relay after re-registration -> false",
            """a2@3000: OnStorageChanged doc local app:k {"v":5} false""",
        ]);
        await page.Clock.RunForAsync(60_000);
        (await Take(page)).ShouldBeEmpty();
    }

    // A success clears the failure state: the prune clock (a failure past pruneAfterMs after a recovery does not prune)
    // and the pending backoff timer (the next failure retries 1 s later, once, not when the old timer was due).
    [Fact]
    public async Task DuckyJs_Recovery_ResetsPruneClockAndPendingBackoff()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'down';
                ducky.watchStorage('a', a, 'app', true, 2000, 1000);
                fire('app:k', '{"v":1}');
                a.mode = 'ok';
                fire('app:k', '{"v":2}');
            }
            """);
        (await Take(page)).ShouldBe([
            """a@0: OnStorageChanged tab local app:k {"v":1} false""",
            """a@0: OnStorageChanged tab local app:k {"v":2} false""",
            "a@0: OnStorageChanged tab local null null false",
        ]);
        await page.Clock.RunForAsync(3_000);
        (await Take(page)).ShouldBeEmpty();

        await page.EvaluateAsync("() => { a.mode = 'down'; fire('app:k', '{\"v\":3}'); }");
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'a')")).ShouldBeTrue();
        await page.Clock.RunForAsync(1_000);
        (await Take(page)).ShouldBe([
            """a@3000: OnStorageChanged tab local app:k {"v":3} false""",
            "a@4000: OnStorageChanged tab local null null false",
        ]);

        // The retry timer is now due at 6000; a success at 4500 and a failure right after retry at 5500 only.
        await page.Clock.RunForAsync(500);
        await page.EvaluateAsync("() => { a.mode = 'ok'; fire('app:k', '{\"v\":4}'); }");
        await page.EvaluateAsync("() => { a.mode = 'down'; fire('app:k', '{\"v\":5}'); }");
        await page.Clock.RunForAsync(1_000);
        (await Take(page)).ShouldBe([
            """a@4500: OnStorageChanged tab local app:k {"v":4} false""",
            "a@4500: OnStorageChanged tab local null null false",
            """a@4500: OnStorageChanged tab local app:k {"v":5} false""",
            "a@5500: OnStorageChanged tab local null null false",
        ]);
        await page.Clock.RunForAsync(500);
        (await Take(page)).ShouldBeEmpty();
    }

    // Blazor Server never settles an invocation sent while its circuit is disconnected (§11.7): a delivery unsettled after
    // 1 s counts as failed, so its pair is re-read on the next success, and an id whose deliveries all hang is pruned at the
    // cap (failures at 1, 3, 6, 11, 20, 37 and 68 s, each 1 s after its retry; 68 s is the first past 1 + 60 s).
    [Fact]
    public async Task DuckyJs_UnsettledDelivery_CountsAsFailed()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'hold';
                ducky.watchStorage('a', a, 'app', true, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{"v":1}', 'w');
            }
            """);
        await page.Clock.RunForAsync(999);
        await page.EvaluateAsync("() => { a.mode = 'ok'; fire('app:k', '{\"v\":2}'); a.mode = 'hold'; }");
        await page.Clock.RunForAsync(1);
        await page.EvaluateAsync("() => { a.mode = 'ok'; fire('app:k', '{\"v\":3}'); }");
        await page.Clock.RunForAsync(120_000);
        (await Take(page)).ShouldBe([
            """a@0: OnStorageChanged doc local app:k {"v":1} false""",
            """a@999: OnStorageChanged tab local app:k {"v":2} false""",
            """a@1000: OnStorageChanged tab local app:k {"v":3} false""",
            "a@1000: OnStorageChanged doc local null null false",
        ]);

        await page.EvaluateAsync("""
            () => {
                window.t0 = Date.now();
                window.p = makeRef('p');
                p.mode = 'hold';
                ducky.watchStorage('p', p, 'app', false, 60000, 1000);
                ducky.storageSet('session', 'app:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(67_999);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'p')")).ShouldBeTrue();
        await page.Clock.RunForAsync(1);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'p')")).ShouldBeFalse();
        await page.Clock.RunForAsync(120_000);
        (await Take(page)).Where(c => c.StartsWith('p')).ShouldBe([
            "p@0: OnStorageChanged doc session app:k {} false",
            .. _hungRetries.Select(at => $"p@{at}: OnStorageChanged doc session null null false"),
        ]);
    }

    // The fast path (WebAssembly, or Server with DetailedErrors; §11.7): a rejection saying the .NET object is no longer
    // tracked prunes at once, and a connection error does not.
    [Fact]
    public async Task DuckyJs_UntrackedObject_PrunesId()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.g = makeRef('g');
                window.d = makeRef('d');
                g.mode = 'gone';
                d.mode = 'down';
                ducky.watchStorage('g', g, 'app', true, 600000, 1000);
                ducky.watchStorage('d', d, 'app', true, 600000, 1000);
                fire('app:k', '{}');
            }
            """);
        (await Take(page)).ShouldBe(["g@0: OnStorageChanged tab local app:k {} false", "d@0: OnStorageChanged tab local app:k {} false"]);

        var after = await page.EvaluateAsync<string[]>("""
            () => {
                const out = [`set by g -> ${ducky.storageSet('local', 'app:k', '{}', 'g')}`, `set by d -> ${ducky.storageSet('local', 'app:k', '{}', 'd')}`];
                fire('app:k', '{}');
                return [...out, ...take()];
            }
            """);
        after.ShouldBe([
            "set by g -> false",
            "set by d -> true",
            "d@0: OnStorageChanged doc local app:k {} false",
            "d@0: OnStorageChanged tab local app:k {} false",
        ]);
        await page.Clock.RunForAsync(1_000);
        (await Take(page)).ShouldBe([
            "d@1000: OnStorageChanged tab local null null false",
            "d@1000: OnStorageChanged doc local null null false",
        ]);
    }

    // A notification that settles after its id was unwatched, and the id registered again, changes nothing: neither the
    // stale success (which would re-read the old registration's failed pair) nor the stale untracked-object rejection
    // (which would prune the new registration).
    [Fact]
    public async Task DuckyJs_DeliverySettledAfterUnwatch_Ignored()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.h = makeRef('h');
                h.mode = 'down';
                ducky.watchStorage('h', h, 'app', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{"v":1}', 'w');
            }
            """);
        await page.EvaluateAsync("""
            () => {
                h.mode = 'hold';
                ducky.storageSet('local', 'app:k', '{"v":2}', 'w');
                ducky.storageSet('local', 'app:k', '{"v":3}', 'w');
                ducky.unwatchStorage('h');
                ducky.unwatchStorage('h');
                window.n = makeRef('n');
                n.mode = 'down';
                ducky.watchStorage('h', n, 'app', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{"v":4}', 'w');
                h.pending[0].reject(new Error("There is no tracked object with id '3'."));
                h.pending[1].resolve();
            }
            """);
        (await Take(page)).ShouldBe([
            """h@0: OnStorageChanged doc local app:k {"v":1} false""",
            """h@0: OnStorageChanged doc local app:k {"v":2} false""",
            """h@0: OnStorageChanged doc local app:k {"v":3} false""",
            """n@0: OnStorageChanged doc local app:k {"v":4} false""",
        ]);
        await page.Clock.RunForAsync(500);
        (await Take(page)).ShouldBeEmpty();
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:k', '{}', 'h')")).ShouldBeTrue();
        await page.Clock.RunForAsync(500);
        (await Take(page)).ShouldBe(["n@1000: OnStorageChanged doc local null null false"]);
    }

    // "Longer than pruneAfterMs" (§11.7): a failure exactly at the cap keeps the id, the next one prunes it.
    [Fact]
    public async Task DuckyJs_FailureExactlyAtCap_KeepsId()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'down';
                ducky.watchStorage('a', a, 'app', false, 1000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(1_000);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'a')")).ShouldBeTrue();
        await page.Clock.RunForAsync(2_000);
        (await Take(page)).ShouldBe([
            "a@0: OnStorageChanged doc local app:k {} false",
            "a@1000: OnStorageChanged doc local null null false",
            "a@3000: OnStorageChanged doc local null null false",
        ]);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'a')")).ShouldBeFalse();
    }

    // A delivery already counted as failed still settles: its late success recovers (the pair is re-read at once, and the
    // armed retry is cancelled), and its late untracked-object rejection prunes.
    [Fact]
    public async Task DuckyJs_LateSettlement_StillCounts()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                window.g = makeRef('g');
                a.mode = g.mode = 'hold';
                ducky.watchStorage('a', a, 'app', false, 600000, 1000);
                ducky.watchStorage('g', g, 'other', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
                ducky.storageSet('local', 'other:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(1_000);
        await page.EvaluateAsync("""
            () => {
                a.mode = 'ok';
                a.pending[0].resolve();
                g.pending[0].reject(new Error("There is no tracked object with id '3'."));
            }
            """);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'g')")).ShouldBeFalse();
        await page.Clock.RunForAsync(60_000);
        (await Take(page)).ShouldBe([
            "a@0: OnStorageChanged doc local app:k {} false",
            "g@0: OnStorageChanged doc local other:k {} false",
            "a@1000: OnStorageChanged doc local null null false",
            "a@1000: OnStorageChanged doc local app:j {} false",
        ]);
    }

    // A delivery slower than 1 s is late, not lost (§11.7): the keyed event's late success causes one re-read, and that
    // re-read's own late success ends it, even when the retries outrun the replies.
    [Fact]
    public async Task DuckyJs_SlowDelivery_ReReadsOnce()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                for (const [name, latency] of [['s', 1500], ['t', 3000], ['u', 100000]]) {
                    const ref = makeRef(name);
                    ref.mode = 'slow';
                    ref.latency = latency;
                    ducky.watchStorage(name, ref, 'app', false, 600000, 1000);
                }
                ducky.storageSet('local', 'app:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(300_000);
        var calls = await Take(page);
        string[] Expected(string name, params int[] reReads) => reReads
            .Select(at => $"{name}@{at}: OnStorageChanged doc local null null false")
            .Prepend($"{name}@0: OnStorageChanged doc local app:k {{}} false")
            .ToArray();
        calls.Where(c => c.StartsWith('s')).ShouldBe(Expected("s", 1_500));
        calls.Where(c => c.StartsWith('t')).ShouldBe(Expected("t", 2_000, 4_000));
        calls.Where(c => c.StartsWith('u')).ShouldBe(Expected("u", 2_000, 5_000, 10_000, 19_000, 36_000, 67_000, 98_000, 100_000));

        await page.Clock.RunForAsync(120_000);
        (await Take(page)).ShouldBeEmpty();
    }

    // A re-read vouches only for what was sent before it: a delivery sent after it and lost needs another re-read, even
    // when an older delivery's failure is recorded later still.
    [Fact]
    public async Task DuckyJs_FailureAfterReRead_ReReadsAgain()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'hold';
                ducky.watchStorage('a', a, 'app', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{"v":1}', 'w');
            }
            """);
        await page.Clock.RunForAsync(1_500);
        await page.EvaluateAsync("""() => ducky.storageSet('local', 'app:k', '{"v":2}', 'w')""");
        await page.Clock.RunForAsync(600);
        await page.EvaluateAsync("""() => { a.mode = 'down'; ducky.storageSet('local', 'app:k', '{"v":3}', 'w'); a.mode = 'hold'; }""");
        await page.Clock.RunForAsync(400);
        await page.EvaluateAsync("() => { a.mode = 'ok'; a.pending[2].resolve(); }");
        await page.Clock.RunForAsync(120_000);

        (await Take(page)).ShouldBe([
            """a@0: OnStorageChanged doc local app:k {"v":1} false""",
            """a@1500: OnStorageChanged doc local app:k {"v":2} false""",
            "a@2000: OnStorageChanged doc local null null false",
            """a@2100: OnStorageChanged doc local app:k {"v":3} false""",
            "a@2500: OnStorageChanged doc local null null false",
        ]);
    }

    // No second re-read of a pair while one is out and not yet failed (§11.7): a stale re-read (counted as failed at 3 s,
    // and older than the event lost at 3.5 s) that settles late, by success (a) or rejection (b), while the next re-read
    // is out, sends none on the recovery that follows; the outstanding re-read then settles the pair.
    [Fact]
    public async Task DuckyJs_StaleReReadSettlement_SendsNoSecondReRead()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                window.b = makeRef('b');
                a.mode = b.mode = 'hold';
                ducky.watchStorage('a', a, 'app', false, 600000, 1000);
                ducky.watchStorage('b', b, 'other', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
                ducky.storageSet('local', 'other:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(2_500);
        await page.EvaluateAsync("""
            () => {
                ducky.storageSet('local', 'app:k', '{}', 'w');
                ducky.storageSet('local', 'other:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(2_500);
        await page.EvaluateAsync("""
            async () => {
                a.pending[1].resolve();
                b.pending[1].reject(new Error('Cannot send data if the connection is not in the \'Connected\' State.'));
                await Promise.resolve();
                b.mode = 'ok';
                ducky.storageSet('local', 'other:j', '{}', 'w');
            }
            """);
        await page.EvaluateAsync("() => { a.pending[3].resolve(); b.pending[3].resolve(); }");
        await page.Clock.RunForAsync(120_000);

        var calls = await Take(page);
        calls.Where(c => c.StartsWith('a')).ShouldBe([
            "a@0: OnStorageChanged doc local app:k {} false",
            "a@2000: OnStorageChanged doc local null null false",
            "a@2500: OnStorageChanged doc local app:k {} false",
            "a@5000: OnStorageChanged doc local null null false",
        ]);
        calls.Where(c => c.StartsWith('b')).ShouldBe([
            "b@0: OnStorageChanged doc local other:k {} false",
            "b@2000: OnStorageChanged doc local null null false",
            "b@2500: OnStorageChanged doc local other:k {} false",
            "b@5000: OnStorageChanged doc local null null false",
            "b@5000: OnStorageChanged doc local other:j {} false",
        ]);
    }

    // A re-read that hangs after its pair was settled is not a failure: it starts no prune clock that a later failure
    // would find already past the cap.
    [Fact]
    public async Task DuckyJs_RedundantReRead_NeverCountsAsFailure()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'hold';
                ducky.watchStorage('a', a, 'app', false, 10000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(5_500);
        await page.EvaluateAsync("() => a.pending[1].resolve()");
        await page.Clock.RunForAsync(20_500);
        await page.EvaluateAsync("() => { a.mode = 'down'; ducky.storageSet('local', 'app:j', '{}', 'w'); }");

        (await Take(page)).ShouldBe([
            "a@0: OnStorageChanged doc local app:k {} false",
            "a@2000: OnStorageChanged doc local null null false",
            "a@5000: OnStorageChanged doc local null null false",
            "a@26000: OnStorageChanged doc local app:j {} false",
        ]);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:k', '{}', 'a')")).ShouldBeTrue();
    }

    // A delivery left unsettled across unwatch never counts against the id's new registration: no retry on the disposed
    // ref, and no prune of the new registration by the old one's cap.
    [Fact]
    public async Task DuckyJs_DeliveryUnsettledAcrossUnwatch_Ignored()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.h = makeRef('h');
                window.i = makeRef('i');
                h.mode = 'hold';
                i.mode = 'down';
                ducky.watchStorage('h', h, 'app', false, 600000, 1000);
                ducky.watchStorage('i', i, 'other', false, 500, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
                ducky.storageSet('local', 'other:k', '{}', 'w');
            }
            """);
        await page.EvaluateAsync("""
            () => {
                i.mode = 'hold';
                ducky.storageSet('local', 'other:k', '{}', 'w');
                ducky.unwatchStorage('h');
                ducky.unwatchStorage('i');
                ducky.watchStorage('h', makeRef('n'), 'app', false, 600000, 1000);
                ducky.watchStorage('i', makeRef('m'), 'other', false, 600000, 1000);
            }
            """);
        await page.Clock.RunForAsync(60_000);

        (await Take(page)).ShouldBe([
            "h@0: OnStorageChanged doc local app:k {} false",
            "i@0: OnStorageChanged doc local other:k {} false",
            "i@0: OnStorageChanged doc local other:k {} false",
        ]);
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'app:j', '{}', 'h')")).ShouldBeTrue();
        (await page.EvaluateAsync<bool>("() => ducky.storageSet('local', 'other:j', '{}', 'i')")).ShouldBeTrue();
    }

    // storageSet writes before it relays: a value storage refused is never announced to the other stores.
    [Fact]
    public async Task DuckyJs_StorageSet_RefusedWriteThrowsAndRelaysNothing()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var result = await page.EvaluateAsync<string[]>("""
            () => {
                ducky.watchStorage('a', makeRef('a'), 'app', false, 600000, 1000);
                Storage.prototype.setItem = () => { throw new Error('quota'); };
                const out = [];
                try {
                    ducky.storageSet('local', 'app:k', '{}', 'w');
                    out.push('no throw');
                } catch (e) {
                    out.push(e.message);
                }
                return [...out, ...take()];
            }
            """);

        result.ShouldBe(["quota"]);
    }

    // Both answer true: .NET calls every export through a typed InvokeAsync, which faults on an undefined result (§11.9).
    [Fact]
    public async Task DuckyJs_WatchExports_ReturnTrue()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var results = await page.EvaluateAsync<bool[]>("""
            () => [
                ducky.watchStorage('a', makeRef('a'), 'app', false, 600000, 1000),
                ducky.watchStorage('a', makeRef('b'), 'app', false, 600000, 1000),
                ducky.unwatchStorage('a'),
                ducky.unwatchStorage('a'),
            ]
            """);

        results.ShouldBe([true, true, true, true]);
    }

    // A forwarded clear() (key null) is an event, not a re-read: its loss, rejected (d) or unsettled (h), makes
    // (tab, local) owed, and the pair is re-read once deliveries succeed again (§11.7).
    [Fact]
    public async Task DuckyJs_FailedClearEvent_ReReadsAfterReconnect()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.d = makeRef('d');
                window.h = makeRef('h');
                d.mode = 'down';
                h.mode = 'hold';
                ducky.watchStorage('d', d, 'app', true, 600000, 1000);
                ducky.watchStorage('h', h, 'app', true, 600000, 1000);
                fire(null, null);
            }
            """);
        await page.Clock.RunForAsync(1_000);
        await page.EvaluateAsync("() => { d.mode = 'ok'; h.mode = 'ok'; }");
        await page.Clock.RunForAsync(120_000);

        (await Take(page)).ShouldBe([
            "d@0: OnStorageChanged tab local null null false",
            "h@0: OnStorageChanged tab local null null false",
            "d@1000: OnStorageChanged tab local null null false",
            "h@2000: OnStorageChanged tab local null null false",
            "d@3000: OnStorageChanged tab local null null false",
        ]);
    }

    // A forwarded clear() that succeeds is a re-read sent after the failed event: it settles the pair, so no resync follows.
    [Fact]
    public async Task DuckyJs_SucceededClearEvent_SettlesOwedPair()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.c = makeRef('c');
                c.mode = 'down';
                ducky.watchStorage('c', c, 'app', true, 600000, 1000);
                fire('app:k', '{"v":1}');
                c.mode = 'ok';
                fire(null, null);
            }
            """);
        await page.Clock.RunForAsync(60_000);

        (await Take(page)).ShouldBe([
            """c@0: OnStorageChanged tab local app:k {"v":1} false""",
            "c@0: OnStorageChanged tab local null null false",
        ]);
    }

    // The late success of the failed clear itself was not sent after its own failure: the pair stays owed and is re-read.
    [Fact]
    public async Task DuckyJs_LateSuccessOfFailedClearEvent_StillReReads()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.k = makeRef('k');
                k.mode = 'hold';
                ducky.watchStorage('k', k, 'app', true, 600000, 1000);
                fire(null, null);
            }
            """);
        await page.Clock.RunForAsync(1_500);
        await page.EvaluateAsync("() => { k.mode = 'ok'; k.pending[0].resolve(); }");
        await page.Clock.RunForAsync(60_000);

        (await Take(page)).ShouldBe([
            "k@0: OnStorageChanged tab local null null false",
            "k@1500: OnStorageChanged tab local null null false",
        ]);
    }

    // An event counted as failed while it hung is not failed again by its late rejection (§11.7): once a re-read sent
    // after it has settled the pair, the pair stays settled, with no retry and no prune clock.
    [Fact]
    public async Task DuckyJs_LateRejectionOfCountedEvent_SendsNoSecondReRead()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync("""
            () => {
                window.a = makeRef('a');
                a.mode = 'hold';
                ducky.watchStorage('a', a, 'app', false, 600000, 1000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
            }
            """);
        await page.Clock.RunForAsync(1_000);
        await page.EvaluateAsync("""
            async () => {
                a.mode = 'ok';
                ducky.storageSet('local', 'app:j', '{}', 'w');
                for (let i = 0; i < 10; i++) await Promise.resolve();
                a.mode = 'down';
                a.pending[0].reject(new Error('Cannot send data if the connection is not in the \'Connected\' State.'));
            }
            """);
        await page.Clock.RunForAsync(60_000);

        (await Take(page)).ShouldBe([
            "a@0: OnStorageChanged doc local app:k {} false",
            "a@1000: OnStorageChanged doc local app:j {} false",
            "a@1000: OnStorageChanged doc local null null false",
        ]);
    }

    private static Task<string[]> Take(IPage page) => page.EvaluateAsync<string[]>("() => take()");

    // The installed clock runs in real time until paused, and pausing behind its fake now throws: a minute ahead of the
    // read leaves room for any scheduling delay between the two calls (2-vCPU CI, parallel browsers), and fires nothing,
    // as no page timer exists before the Prelude. From here on, time moves only when a spec runs the clock; specs measure
    // from window.t0.
    private static async Task<IPage> OpenAsync(Harness harness)
    {
        var page = await harness.NewPageAsync();
        await page.GotoAsync("index.html");
        var now = await page.EvaluateAsync<long>("Date.now()");
        await page.Clock.PauseAtAsync(DateTimeOffset.FromUnixTimeMilliseconds(now + 60_000).UtcDateTime);
        await page.EvaluateAsync(Prelude);
        return page;
    }
}
