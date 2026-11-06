using Microsoft.Playwright;

namespace Ducky.E2E;

// The Redux DevTools exports of ducky.js (SPEC §11.7, §11.8, §11.9, INV-23) on the static harness page, against a fake
// extension that logs what it receives into window.calls. The clock is paused (§17.2). Each fake DotNetObjectReference
// logs "<ref>@<ms since t0>: <OnMessage arguments>" and answers as its mode says: "ok" resolves, "down" rejects with a
// connection error, "gone" with the untracked-object error, "hold" keeps the promise in pending.
public sealed class DuckyJsDevToolsTests
{
    // The inline budget rides in configJson (devtoolsConnect has no budget argument, §11.9) and never reaches the extension.
    private const string Config = """{"name":"app","maxAge":50,"trace":false,"maxInlineBytes":100}""";

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
                        if (ref.mode === 'down') return Promise.reject(new Error('Cannot send data if the connection is not in the \'Connected\' State.'));
                        if (ref.mode === 'gone') return Promise.reject(new Error("System.ArgumentException: There is no tracked object with id '3'. Perhaps the DotNetObjectReference instance was already disposed."));
                        return Promise.resolve();
                    },
                };
                return ref;
            };
            window.connections = [];
            window.installExtension = () => {
                window.__REDUX_DEVTOOLS_EXTENSION__ = {
                    connect(options) {
                        const connection = {
                            listeners: [],
                            init: state => calls.push(`init ${JSON.stringify(state)}`),
                            send: (action, state) => calls.push(`send ${JSON.stringify(action)} ${JSON.stringify(state)}`),
                            subscribe: listener => { connection.listeners.push(listener); },
                            unsubscribe: () => { connection.listeners = []; calls.push('unsubscribe'); },
                        };
                        calls.push(`connect ${JSON.stringify(options)}`);
                        connections.push(connection);
                        return connection;
                    },
                };
            };
            window.emit = message => connections.at(-1).listeners.forEach(listener => listener(message));
            window.settle = async () => { for (let i = 0; i < 10; i++) await Promise.resolve(); };
            window.pulled = (id, seq) => new TextDecoder().decode(ducky.devtoolsTakeMessage(id, seq));
            window.take = () => calls.splice(0);
        }
        """;

    // Without the extension nothing connects and every other export is inert; the extension is looked up per call, so one
    // installed later is found.
    [Fact]
    public async Task DuckyJs_DevtoolsConnect_NoExtension_ReturnsFalse()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>($$"""
            () => {
                const a = makeRef('a');
                const out = [
                    `connect -> ${ducky.devtoolsConnect('a', a, '{{Config}}', 600000)}`,
                    `send -> ${ducky.devtoolsSend('a', '{"type":"x"}', '{}')}`,
                ];
                ducky.devtoolsInit('a', '{}');
                ducky.devtoolsDisconnect('a');
                out.push(`take -> ${Array.from(ducky.devtoolsTakeMessage('a', 1))}`);
                installExtension();
                out.push(`connect with the extension -> ${ducky.devtoolsConnect('a', a, '{{Config}}', 600000)}`);
                return [...out, ...take()];
            }
            """);

        steps.ShouldBe([
            "connect -> false",
            "send -> false",
            "take -> 0",
            "connect with the extension -> true",
            """connect {"name":"app","maxAge":50,"trace":false}""",
        ]);
    }

    // As for watchStorage (§11.9): a caller that omits the budget gets a rejected call, never a connection that pulls every
    // message by stream. 0 is a budget.
    [Fact]
    public async Task DuckyJs_DevtoolsConnect_BudgetNotANonNegativeInteger_Throws()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>("""
            () => {
                installExtension();
                const out = [];
                for (const budget of ['', ',"maxInlineBytes":-1', ',"maxInlineBytes":1.5', ',"maxInlineBytes":"10"', ',"maxInlineBytes":0']) {
                    let result;
                    try {
                        result = ducky.devtoolsConnect('a', makeRef('a'), `{"name":"app"${budget}}`, 600000);
                    } catch (error) {
                        result = `${error.name}: ${error.message}`;
                    }
                    out.push(`${budget || 'none'} -> ${result}, send ${ducky.devtoolsSend('a', '{}', '{}')}`);
                }
                return [...out, ...take()];
            }
            """);

        steps.ShouldBe([
            "none -> TypeError: devtoolsConnect: maxInlineBytes, send false",
            ""","maxInlineBytes":-1 -> TypeError: devtoolsConnect: maxInlineBytes, send false""",
            ""","maxInlineBytes":1.5 -> TypeError: devtoolsConnect: maxInlineBytes, send false""",
            ""","maxInlineBytes":"10" -> TypeError: devtoolsConnect: maxInlineBytes, send false""",
            ""","maxInlineBytes":0 -> true, send true""",
            """connect {"name":"app"}""",
            "send {} {}",
        ]);
    }

    // init and send hand the extension parsed values, never the pre-serialized strings; ids are independent; a
    // disconnected id unsubscribes once and then reports false.
    [Fact]
    public async Task DuckyJs_DevtoolsInitSendDisconnect_PerId()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>($$$"""
            () => {
                installExtension();
                const out = [];
                const step = (label, result) => out.push(`${label} -> ${result}`, ...take());
                step('connect', ducky.devtoolsConnect('a', makeRef('a'), '{{{Config}}}', 600000));
                step('init', ducky.devtoolsInit('a', '{"cart":{"n":0}}'));
                step('send', ducky.devtoolsSend('a', '{"type":"cart/Add","payload":{"n":1}}', '{"cart":{"n":1}}'));
                step('send for another id', ducky.devtoolsSend('b', '{"type":"x"}', '{}'));
                step('init for another id', ducky.devtoolsInit('b', '{}'));
                step('disconnect', ducky.devtoolsDisconnect('a'));
                step('send after disconnect', ducky.devtoolsSend('a', '{"type":"x"}', '{}'));
                step('disconnect again', ducky.devtoolsDisconnect('a'));
                return out;
            }
            """);

        steps.ShouldBe([
            "connect -> true",
            """connect {"name":"app","maxAge":50,"trace":false}""",
            "init -> undefined",
            """init {"cart":{"n":0}}""",
            "send -> true",
            """send {"type":"cart/Add","payload":{"n":1}} {"cart":{"n":1}}""",
            "send for another id -> false",
            "init for another id -> undefined",
            "disconnect -> undefined",
            "unsubscribe",
            "send after disconnect -> false",
            "disconnect again -> undefined",
        ]);
    }

    // §11.8: a jump crosses inline as {type, payload: {type, actionId|index}}, without the extension's state, however large;
    // any other message crosses inline up to the budget, in UTF-8 bytes of its JSON encoding, and above it is kept under
    // (id, seq) for one devtoolsTakeMessage. "xx" is exactly 100 bytes; "x語" is 100 UTF-16 code units but 102 bytes. A
    // jump is never measured: under a budget of 0 it still crosses inline while any other message is pulled.
    [Fact]
    public async Task DuckyJs_DevToolsLargeMessage_PullBranch()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>($$"""
            async () => {
                installExtension();
                ducky.devtoolsConnect('a', makeRef('a'), '{{Config}}', 600000);
                take();
                const state = JSON.stringify({ big: 'x'.repeat(1000) });
                const imported = s => ({ type: 'DISPATCH', payload: { type: 'IMPORT_STATE', nextLiftedState: { s } } });
                emit({ type: 'START' });
                emit({ type: 'DISPATCH', payload: { type: 'JUMP_TO_STATE', index: 2, actionId: 2 }, state, id: 'x', source: '@devtools-extension' });
                emit({ type: 'DISPATCH', payload: { type: 'JUMP_TO_ACTION', actionId: 3 }, state });
                emit(imported('xx'));
                emit(imported('x語'));
                await settle();
                const out = [
                    ...take(),
                    `take 5 -> ${pulled('a', 5)}`,
                    `take 5 again -> ${Array.from(ducky.devtoolsTakeMessage('a', 5))}`,
                    `take the inline 4 -> ${Array.from(ducky.devtoolsTakeMessage('a', 4))}`,
                ];
                ducky.devtoolsConnect('z', makeRef('z'), '{"maxInlineBytes":0}', 600000);
                take();
                emit({ type: 'DISPATCH', payload: { type: 'JUMP_TO_STATE', index: 1, actionId: 1 }, state });
                emit({ type: 'START' });
                await settle();
                return [...out, ...take(), `take z 2 -> ${pulled('z', 2)}`];
            }
            """);

        steps.ShouldBe([
            """a@0: OnMessage {"type":"START"} 1 false""",
            """a@0: OnMessage {"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","actionId":2,"index":2}} 2 false""",
            """a@0: OnMessage {"type":"DISPATCH","payload":{"type":"JUMP_TO_ACTION","actionId":3}} 3 false""",
            """a@0: OnMessage {"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"s":"xx"}}} 4 false""",
            "a@0: OnMessage null 5 true",
            """take 5 -> {"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"s":"x語"}}}""",
            "take 5 again -> 0",
            "take the inline 4 -> 0",
            """z@0: OnMessage {"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","actionId":1,"index":1}} 1 false""",
            "z@0: OnMessage null 2 true",
            """take z 2 -> {"type":"START"}""",
        ]);
    }

    // An over-budget ACTION is pulled too (only jumps are stripped), and kept until taken.
    [Fact]
    public async Task DuckyJs_DevtoolsLargeAction_KeptUntilTaken()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>($$"""
            async () => {
                installExtension();
                ducky.devtoolsConnect('a', makeRef('a'), '{{Config}}', 600000);
                take();
                emit({ type: 'ACTION', payload: 'y'.repeat(200) });
                await settle();
                return [...take(), `take 1 -> ${pulled('a', 1)}`];
            }
            """);

        steps.ShouldBe([
            "a@0: OnMessage null 1 true",
            "take 1 -> {\"type\":\"ACTION\",\"payload\":\"" + new string('y', 200) + "\"}",
        ]);
    }

    // §11.7: the first failure since the last success starts the prune clock; a connection still failing pruneAfterMs
    // later is dropped though no message arrives meanwhile, and a later failure does not restart the clock. At the cap it
    // is kept. Only the DevTools connection goes: the id's storage registration, whose deliveries succeed meanwhile, has
    // its own cap and stays. A rejected delivery drops its kept message.
    [Fact]
    public async Task DuckyJs_DevtoolsDeliveryFailure_DropsConnectionAfterCap()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);
        const string Budget0 = """{"name":"app","maxInlineBytes":0}""";

        await page.EvaluateAsync($$"""
            async () => {
                installExtension();
                window.a = makeRef('a');
                a.mode = 'down';
                ducky.watchStorage('a', makeRef('s'), 'app', false, 2000, 1000);
                ducky.devtoolsConnect('a', a, '{{Budget0}}', 2000);
                emit({ type: 'START' });
                await settle();
            }
            """);
        await page.Clock.RunForAsync(1_000);
        await page.EvaluateAsync("async () => { emit({ type: 'STOP' }); ducky.storageSet('local', 'app:k', '1', 'w'); await settle(); }");
        await page.Clock.RunForAsync(1_000);
        var atCap = await page.EvaluateAsync<string[]>("""
            () => [
                ...take(),
                `take 1 -> ${Array.from(ducky.devtoolsTakeMessage('a', 1))}`,
                `storage registration -> ${ducky.storageSet('local', 'app:k', '{}', 'a')}`,
                `send -> ${ducky.devtoolsSend('a', '{}', '{}')}`,
                ...take(),
            ]
            """);
        atCap.ShouldBe([
            """connect {"name":"app"}""",
            "a@0: OnMessage null 1 true",
            "a@1000: OnMessage null 2 true",
            "s@1000: OnStorageChanged doc local app:k 1 false",
            "take 1 -> 0",
            "storage registration -> true",
            "send -> true",
            "send {} {}",
        ]);

        await page.Clock.RunForAsync(1);
        var pastCap = await page.EvaluateAsync<string[]>("""
            async () => {
                const out = [
                    ...take(),
                    `send -> ${ducky.devtoolsSend('a', '{}', '{}')}`,
                    `storage registration -> ${ducky.storageSet('local', 'app:k', '{}', 'a')}`,
                ];
                ducky.storageSet('local', 'app:k', '2', 'w');
                await settle();
                return [...out, ...take()];
            }
            """);
        pastCap.ShouldBe([
            "unsubscribe",
            "send -> false",
            "storage registration -> true",
            "s@2001: OnStorageChanged doc local app:k 2 false",
        ]);
    }

    // A delivery that never settles (Blazor Server, circuit disconnected) and no message after it: the prune clock starts
    // when it counts as failed, 1 s after it was sent, and prunes pruneAfterMs after that.
    [Fact]
    public async Task DuckyJs_DevtoolsHeldDelivery_PrunesAtCapWithoutFurtherMessages()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync($$"""
            () => {
                installExtension();
                const h = makeRef('h');
                h.mode = 'hold';
                ducky.devtoolsConnect('h', h, '{{Config}}', 2000);
                emit({ type: 'START' });
            }
            """);
        await page.Clock.RunForAsync(3_000);
        (await page.EvaluateAsync<string[]>("() => [...take(), `send -> ${ducky.devtoolsSend('h', '{}', '{}')}`, ...take()]")).ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            """h@0: OnMessage {"type":"START"} 1 false""",
            "send -> true",
            "send {} {}",
        ]);
        await page.Clock.RunForAsync(1);
        (await page.EvaluateAsync<string[]>("() => [...take(), `send -> ${ducky.devtoolsSend('h', '{}', '{}')}`]")).ShouldBe([
            "unsubscribe",
            "send -> false",
        ]);
    }

    // The untracked-object rejection prunes the id at once, from either side: a DevTools delivery drops the storage
    // registration too, and a storage delivery drops the DevTools connection.
    [Fact]
    public async Task DuckyJs_DevtoolsUntrackedObject_PrunesWholeId()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        var steps = await page.EvaluateAsync<string[]>($$"""
            async () => {
                installExtension();
                const g = makeRef('g');
                g.mode = 'gone';
                ducky.watchStorage('a', makeRef('s'), 'app', false, 600000, 1000);
                ducky.devtoolsConnect('a', g, '{{Config}}', 600000);
                emit({ type: 'START' });
                await settle();
                const out = [...take(), `a send -> ${ducky.devtoolsSend('a', '{}', '{}')}`, `a storage -> ${ducky.storageSet('local', 'app:k', '{}', 'a')}`];
                const s = makeRef('sb');
                s.mode = 'gone';
                ducky.watchStorage('b', s, 'app', false, 600000, 1000);
                ducky.devtoolsConnect('b', makeRef('b'), '{{Config}}', 600000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
                await settle();
                return [...out, ...take(), `b send -> ${ducky.devtoolsSend('b', '{}', '{}')}`];
            }
            """);

        steps.ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            """g@0: OnMessage {"type":"START"} 1 false""",
            "unsubscribe",
            "a send -> false",
            "a storage -> false",
            """connect {"name":"app","maxAge":50,"trace":false}""",
            "sb@0: OnStorageChanged doc local app:k {} false",
            "unsubscribe",
            "b send -> false",
        ]);
    }

    // A delivery still unsettled 1 s after it was sent counts as failed (Blazor Server never settles one sent while the
    // circuit is disconnected); a success, late or not, restarts the prune clock, and a settled delivery's timer never
    // fires a failure.
    [Fact]
    public async Task DuckyJs_DevtoolsUnsettledDelivery_CountsAsFailed()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync($$"""
            async () => {
                installExtension();
                window.h = makeRef('h');
                h.mode = 'hold';
                ducky.devtoolsConnect('h', h, '{{Config}}', 2000);
                emit({ type: 'START' });
            }
            """);
        await page.Clock.RunForAsync(1_500);
        await page.EvaluateAsync("""
            async () => {
                h.pending[0].resolve();
                await settle();
                h.mode = 'down';
            }
            """);
        await page.Clock.RunForAsync(2_500);
        var lateSuccess = await page.EvaluateAsync<string[]>("""
            async () => {
                emit({ type: 'STOP' });
                await settle();
                return [...take(), `send -> ${ducky.devtoolsSend('h', '{}', '{}')}`, ...take()];
            }
            """);
        lateSuccess.ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            """h@0: OnMessage {"type":"START"} 1 false""",
            """h@4000: OnMessage {"type":"STOP"} 2 false""",
            "send -> true",
            "send {} {}",
        ]);

        // The clock the failure at 1000 started was stopped by the late success; the one the failure at 4000 started prunes
        // past the cap, with no message after it.
        await page.Clock.RunForAsync(2_000);
        (await page.EvaluateAsync<bool>("() => ducky.devtoolsSend('h', '{}', '{}')")).ShouldBeTrue();
        await page.Clock.RunForAsync(1);
        (await page.EvaluateAsync<string[]>("() => [...take(), `send -> ${ducky.devtoolsSend('h', '{}', '{}')}`]")).ShouldBe([
            "send {} {}",
            "unsubscribe",
            "send -> false",
        ]);
    }

    // Settled deliveries leave no timer behind: a run of successes, then one failure long after, does not prune.
    [Fact]
    public async Task DuckyJs_DevtoolsSettledDelivery_StartsNoPruneClock()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync($$"""
            async () => {
                installExtension();
                window.a = makeRef('a');
                ducky.devtoolsConnect('a', a, '{{Config}}', 2000);
                emit({ type: 'START' });
                await settle();
            }
            """);
        await page.Clock.RunForAsync(5_000);
        var steps = await page.EvaluateAsync<string[]>("""
            async () => {
                a.mode = 'down';
                emit({ type: 'STOP' });
                await settle();
                return [...take(), `send -> ${ducky.devtoolsSend('a', '{}', '{}')}`, ...take()];
            }
            """);

        steps.ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            """a@0: OnMessage {"type":"START"} 1 false""",
            """a@5000: OnMessage {"type":"STOP"} 2 false""",
            "send -> true",
            "send {} {}",
        ]);
    }

    // A delivery that settles after its id was disconnected and connected again changes nothing: its untracked-object
    // rejection would otherwise prune the new connection, and its unsettled timer count against it. Nor does the prune
    // clock an earlier failure of the old connection started.
    [Fact]
    public async Task DuckyJs_DevtoolsSettlementAfterReconnect_Ignored()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync($$"""
            async () => {
                installExtension();
                window.old = makeRef('old');
                old.mode = 'down';
                ducky.devtoolsConnect('a', old, '{{Config}}', 0);
                emit({ type: 'FAIL' });
                await settle();
                old.mode = 'hold';
                emit({ type: 'START' });
                emit({ type: 'STOP' });
                ducky.devtoolsDisconnect('a');
                ducky.devtoolsConnect('a', makeRef('new'), '{{Config}}', 0);
                old.pending[0].reject(new Error("There is no tracked object with id '3'."));
                await settle();
            }
            """);
        await page.Clock.RunForAsync(1_000);

        (await page.EvaluateAsync<string[]>("() => [...take(), `send -> ${ducky.devtoolsSend('a', '{}', '{}')}`, ...take()]")).ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            """old@0: OnMessage {"type":"FAIL"} 1 false""",
            """old@0: OnMessage {"type":"START"} 2 false""",
            """old@0: OnMessage {"type":"STOP"} 3 false""",
            "unsubscribe",
            """connect {"name":"app","maxAge":50,"trace":false}""",
            "send -> true",
            "send {} {}",
        ]);
    }

    // The storage-side prune at the cap (§11.7) drops the id's DevTools connection too.
    [Fact]
    public async Task DuckyJs_StoragePruneAtCap_DisconnectsDevtools()
    {
        await using var harness = await Harness.StartAsync();
        var page = await OpenAsync(harness);

        await page.EvaluateAsync($$"""
            async () => {
                installExtension();
                const s = makeRef('s');
                s.mode = 'down';
                ducky.watchStorage('a', s, 'app', false, 2000, 1000);
                ducky.devtoolsConnect('a', makeRef('a'), '{{Config}}', 600000);
                ducky.storageSet('local', 'app:k', '{}', 'w');
                await settle();
            }
            """);
        await page.Clock.RunForAsync(3_000);

        (await page.EvaluateAsync<string[]>("() => [...take().filter(c => !c.startsWith('s@')), `send -> ${ducky.devtoolsSend('a', '{}', '{}')}`]")).ShouldBe([
            """connect {"name":"app","maxAge":50,"trace":false}""",
            "unsubscribe",
            "send -> false",
        ]);
    }

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
