using System.Globalization;
using System.Text;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DevTools;

// SPEC §11.8 history and inbound (INV-28): the drainer records (devToolsIndex, StateSnapshot) per @@INIT and send, capped at
// MaxAge; OnMessage reads one inline message: JUMP_TO_STATE/JUMP_TO_ACTION restore the typed snapshot the index names
// (except @ducky/*) and engage time travel off the latest, which vetoes Local and Effect actions; RESET restores the initial
// states; IMPORT_STATE restores the imported state's last computed state per slice; everything else is ignored (Debug).
public sealed class DevToolsInboundTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task DevTools_JumpToRecordState_RestoresReferenceEqualSlices()
    {
        // The 1.x regression: a jump went through JSON and restored copies (or nothing) for record states. History is typed.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync(); // 0 @@INIT, 1 StoreInitialized
        await Dispatch(x, new SetLabel("one")); // 2
        var (label, counter) = (x.Store.State.Get<Label>(), x.Store.State.Get<Counter>());
        await Dispatch(x, new Increment()); // 3
        var latest = x.Store.State.Get<Counter>();
        await Dispatch(x, new HydrationFailed("TimeoutException", "slow")); // 4: @ducky/persistence is Failed from here on

        await Send(x, Jump("JUMP_TO_STATE", 2));
        x.Store.State.Get<Label>().ShouldBeSameAs(label);
        x.Store.State.Get<Counter>().ShouldBeSameAs(counter);
        x.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed); // library slices are never restored

        await Send(x, Jump("JUMP_TO_ACTION", 4));
        x.Store.State.Get<Counter>().ShouldBeSameAs(latest);
        x.Store.State.Get<Label>().ShouldBeSameAs(label);

        // A jump's restore is never sent: the extension already shows the jump, and its index would no longer match.
        x.Middleware.Sends.ShouldBe(4);
        x.SentTypes.Count.ShouldBe(4);
    }

    [Fact]
    public async Task DevTools_DuringTimeTravel_UserActionVetoed()
    {
        var gate = new TaskCompletionSource();
        var late = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var x = new DevToolsHarness(d => d.AddEffect(new LateIncrement(gate, late)).UseDevTools());
        await x.InitializeAsync();
        await Dispatch(x, new Increment()); // 2
        (await x.Store.DispatchAsync(new SetLabel("go")).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced); // 3; its effect waits
        x.Middleware.Sends.ShouldBe(3);

        // Off the latest: Local and Effect actions are vetoed, late effect results included; the state stays the jumped one.
        await Message(x, Jump("JUMP_TO_ACTION", 2));
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);
        gate.SetResult();
        (await late.Task.WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);
        await x.Store.WhenIdleAsync(Ct);
        x.Store.State.Get<Counter>().Value.ShouldBe(1);
        x.Store.State.Get<Label>().Text.ShouldBe("");
        x.Middleware.Sends.ShouldBe(3);

        // Library origins are never offered to MayDispatch: a restore still reduces, and is sent as the new latest.
        x.Store.Restore(new Dictionary<string, object> { ["label"] = new Label("restored") }, Origin.Hydration); // 4
        await x.Store.WhenIdleAsync(Ct);
        x.Store.State.Get<Label>().Text.ShouldBe("restored");
        x.Middleware.Sends.ShouldBe(4);

        // Jumping to an earlier entry keeps time travel engaged; jumping back to the latest releases it.
        await Send(x, Jump("JUMP_TO_STATE", 3));
        x.Store.State.Get<Label>().Text.ShouldBe("go");
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);
        await Send(x, Jump("JUMP_TO_STATE", 4));
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(2);
        x.Store.State.Get<Label>().Text.ShouldBe("restored");
        x.SentTypes[^1].ShouldBe("Ducky.Blazor.Tests.Core.Increment");
        x.Middleware.Sends.ShouldBe(5);
    }

    [Theory]
    [InlineData("unknown-id")]
    [InlineData("send-rejected")]
    public async Task DevTools_GoingInertDuringTimeTravel_ReleasesVeto(string failure)
    {
        // Inert, DevTools can deliver no jump back to the latest: the veto must not outlive the connection (§11.8).
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        await Dispatch(x, new Increment()); // 2
        await Dispatch(x, new Increment()); // 3
        await Send(x, Jump("JUMP_TO_ACTION", 2));
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);

        x.OnSend = failure == "unknown-id" ? static _ => false : static _ => throw new JSException("rejected");
        x.Store.Restore(new Dictionary<string, object> { ["label"] = new Label("restored") }, Origin.Hydration); // a library send
        await x.Store.WhenIdleAsync(Ct);

        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(2);
        x.Middleware.Sends.ShouldBe(4); // inert: the Increment is no longer sent
    }

    [Fact]
    public async Task DevTools_JumpAfterFilteredActions_RestoresMatchingEntry()
    {
        // The fake extension numbers what it receives, as the extension's lifted state does: @@INIT is 0, then +1 per send.
        // A filtered action is never sent, so it takes no index (ActionContext.Id would count it).
        List<long> received = [];
        await using var x = new DevToolsHarness(static d => d.UseDevTools(static o => o.Filter = static c => c.Action is not SetLabel));
        x.OnSend = _ =>
        {
            received.Add(received.Count + 1);
            return true;
        };
        await x.InitializeAsync();
        await Dispatch(x, new Increment());
        var first = (x.Store.State.Get<Counter>(), x.Store.State.Get<Label>());
        await Dispatch(x, new SetLabel("hidden")); // filtered
        await Dispatch(x, new Increment());
        var second = (x.Store.State.Get<Counter>(), x.Store.State.Get<Label>());
        received.ShouldBe([1, 2, 3]);

        await Send(x, Jump("JUMP_TO_ACTION", received[1]));
        x.Store.State.Get<Counter>().ShouldBeSameAs(first.Item1);
        x.Store.State.Get<Label>().ShouldBeSameAs(first.Item2);

        await Send(x, Jump("JUMP_TO_ACTION", received[2]));
        x.Store.State.Get<Counter>().ShouldBeSameAs(second.Item1);
        x.Store.State.Get<Label>().ShouldBeSameAs(second.Item2);
        x.Store.State.Get<Label>().Text.ShouldBe("hidden");

        // @@INIT is entry 0; a jump without actionId falls back to the extension's index.
        await Send(x, """{"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","index":0}}""");
        x.Store.State.Get<Counter>().Value.ShouldBe(0);
        x.Store.State.Get<Label>().Text.ShouldBe("");
        await Send(x, """{"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","actionId":2,"index":0}}""");
        x.Store.State.Get<Counter>().ShouldBeSameAs(first.Item1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // a RESET during time travel releases it
    public async Task DevTools_Reset_RestoresInitialStates(bool timeTravel)
    {
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        await Dispatch(x, new Increment());
        await Dispatch(x, new SetLabel("changed"));
        await Dispatch(x, new HydrationFailed("TimeoutException", "slow"));
        if (timeTravel)
        {
            await Send(x, Jump("JUMP_TO_ACTION", 2));
        }

        await Send(x, """{"type":"DISPATCH","payload":{"type":"RESET"}}""");

        x.Store.State.Get<Counter>().ShouldBeSameAs(x.Store.InitialState.Get<Counter>());
        x.Store.State.Get<Label>().ShouldBeSameAs(x.Store.InitialState.Get<Label>());
        x.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed); // library slices are never reset
        x.Middleware.Sends.ShouldBe(4);

        // RESET is no jump: user actions reduce again.
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(1);
    }

    [Fact]
    public async Task DevTools_ImportState_RestoresLastComputedStatePerSlice()
    {
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        await Dispatch(x, new HydrationFailed("TimeoutException", "slow"));
        await Send(x, Jump("JUMP_TO_ACTION", 0)); // time travel, which the import releases
        x.Logs.Clear();

        // The last computed state, per slice by its declared type; @ducky/* is skipped before it is deserialized (a
        // DevToolsJson payload, which has no type info for PersistenceState), and so are unknown keys.
        await Send(x, """
            {"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[
              {"state":{"counter":{"Value":1}}},
              {"state":{"counter":{"Value":5},"label":{"Text":"imported"},"@ducky/persistence":{"status":"Hydrated"},"other":1}}]}}}
            """);
        x.Store.State.Get<Counter>().Value.ShouldBe(5);
        x.Store.State.Get<Label>().Text.ShouldBe("imported");
        x.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);
        x.Logs.GetSnapshot().ShouldNotContain(static r => r.Level >= LogLevel.Warning);
        x.Logs.Clear();

        // A slice whose payload fails to deserialize is skipped with a Warning; the others still restore.
        await Send(x, """{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[{"state":{"counter":{"Value":7},"label":42}}]}}}""");
        x.Store.State.Get<Counter>().Value.ShouldBe(7);
        x.Store.State.Get<Label>().Text.ShouldBe("imported");
        x.Logs.GetSnapshot().Where(static r => r.Id.Id == 1071).ShouldHaveSingleItem().Message.ShouldContain("'label'");

        // An import's restore is never sent, as for a jump.
        x.Middleware.Sends.ShouldBe(2);
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(8);
    }

    [Theory]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"COMMIT"}}""", "COMMIT")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"ROLLBACK","state":"{}"}}""", "ROLLBACK")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"SWEEP"}}""", "SWEEP")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"TOGGLE_ACTION","id":1}}""", "TOGGLE_ACTION")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE"}}""", "JUMP_TO_STATE")] // no index
    [InlineData("""{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[]}}}""", "IMPORT_STATE")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[{}]}}}""", "IMPORT_STATE")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"IMPORT_STATE"}}""", "IMPORT_STATE")]
    [InlineData("""{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{}}}""", "IMPORT_STATE")]
    [InlineData("""{"type":"DISPATCH"}""", null)]
    [InlineData("""{"type":"START"}""", null)]
    [InlineData("""{"type":"ACTION","payload":{"type":"RESET"}}""", null)] // only DISPATCH carries extension commands
    [InlineData("""{"type":"ACTION","payload":"{\"type\":\"RESET\"}"}""", null)] // malformed for the wire shape
    [InlineData("not json", null)]
    [InlineData("null", null)]
    [InlineData(null, null)]
    public async Task DevTools_OtherMessages_IgnoredWithDebugLog(string? json, string? type)
    {
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        await Dispatch(x, new Increment());
        var state = x.Store.State;

        await x.Middleware.OnMessageAsync(json, 7, tooLarge: json is null);
        await x.Store.WhenIdleAsync(Ct);

        x.Store.State.ShouldBeSameAs(state);
        var record = x.Logs.GetSnapshot().Where(static r => r.Id.Id == 2065).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldBe($"Redux DevTools message 7 ignored (type '{type ?? "(null)"}', too large: {json is null})");
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
    }

    // Per jump target -1..6, the entry restored (-1: ignored, Debug 2066), by actionId and by index. The extension's index is a
    // position in its lifted state; once MaxAge is exceeded it keeps slot 0 (actionId 0) as the committed state and the
    // latest MaxAge-1 actions, so a full history lines up slot for slot. Before that, positions are ids.
    [Theory]
    [InlineData(3, new long[] { -1, 3, -1, -1, 3, 4, 5, -1 }, new long[] { -1, 3, 4, 5, -1, -1, -1, -1 })]
    [InlineData(1, new long[] { -1, 5, -1, -1, -1, -1, 5, -1 }, new long[] { -1, 5, -1, -1, -1, -1, -1, -1 })]
    [InlineData(0, new long[] { -1, -1, -1, -1, -1, -1, 5, -1 }, new long[] { -1, 5, -1, -1, -1, -1, -1, -1 })] // the history always holds the latest entry
    [InlineData(10, new long[] { -1, 0, 1, 2, 3, 4, 5, -1 }, new long[] { -1, 0, 1, 2, 3, 4, 5, -1 })]
    public async Task DevTools_History_CappedAtMaxAge_EvictedIndexIgnored(int maxAge, long[] byActionId, long[] byIndex)
    {
        await using var x = new DevToolsHarness(d => d.UseDevTools(o => o.MaxAge = maxAge));
        await x.InitializeAsync(); // 0, 1
        List<Counter> counters = [x.Store.State.Get<Counter>(), x.Store.State.Get<Counter>()];
        for (var i = 0; i < 4; i++)
        {
            await Dispatch(x, new Increment()); // 2, 3, 4, 5
            counters.Add(x.Store.State.Get<Counter>());
        }

        for (var target = -1L; target <= 6; target++)
        {
            await Expect(target, Jump("JUMP_TO_ACTION", target), byActionId[target + 1]);
            await Expect(target, $$$"""{"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","index":{{{target}}}}}""", byIndex[target + 1]);
        }

        async Task Expect(long target, string jump, long restored)
        {
            var before = x.Store.State.Get<Counter>();
            x.Logs.Clear();
            await Send(x, jump);
            if (restored >= 0)
            {
                x.Store.State.Get<Counter>().ShouldBeSameAs(counters[(int)restored]);
                x.EventIds(LogLevel.Debug).ShouldNotContain(2066);
            }
            else
            {
                x.Store.State.Get<Counter>().ShouldBeSameAs(before);
                var record = x.Logs.GetSnapshot().Where(static r => r.Id.Id == 2066).ShouldHaveSingleItem();
                record.Level.ShouldBe(LogLevel.Debug);
                record.Message.ShouldBe($"Redux DevTools jump to index {target} ignored: the history no longer holds it (MaxAge)");
            }
        }
    }

    [Fact]
    public async Task DevTools_JumpWithEmptyHistory_IgnoredAsEvicted()
    {
        // Connected with @@INIT still pending (a throwing sanitizer), nothing is recorded: a jump names no entry, and nothing
        // is vetoed. RESET still applies there.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(static o => o.StateSanitizer = static (_, _) => throw new InvalidOperationException("sanitizer")));
        await x.InitializeAsync();
        await Dispatch(x, new Increment());
        x.Inits.ShouldBeEmpty();
        var state = x.Store.State;

        await Send(x, Jump("JUMP_TO_STATE", 0));
        x.Store.State.ShouldBeSameAs(state);
        x.EventIds(LogLevel.Debug).ShouldContain(2066);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2065);
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(2);

        await Send(x, """{"type":"DISPATCH","payload":{"type":"RESET"}}""");
        x.Store.State.Get<Counter>().ShouldBeSameAs(x.Store.InitialState.Get<Counter>());
        x.EventIds(LogLevel.Debug).ShouldNotContain(2065);
    }

    [Theory]
    [InlineData("unknown-id")]
    [InlineData("send-rejected")]
    [InlineData("connect-timeout")]
    [InlineData("extension-absent")]
    public async Task DevTools_Inert_InboundMessagesIgnored(string failure)
    {
        // Inert, the history may no longer match the extension's index (a failed send) or the extension may know a connect
        // .NET gave up on, yet ducky.js may still forward the monitor's messages: none of them touches the state (§11.8).
        var timeout = TimeSpan.FromSeconds(2);
        var never = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var x = new DevToolsHarness(static d => d.UseDevTools(), blazor: o => o.HydrationTimeout = timeout);
        x.OnConnect = failure switch
        {
            "connect-timeout" => _ => never.Task,
            "extension-absent" => static _ => false,
            _ => static _ => true,
        };
        var init = x.InitializeAsync();
        if (failure == "connect-timeout")
        {
            x.Time.Advance(timeout);
        }

        await init.WaitAsync(_bound, Ct);
        await Dispatch(x, new Increment()); // 2 when connected
        if (failure is "unknown-id" or "send-rejected")
        {
            x.OnSend = failure == "unknown-id" ? static _ => false : static _ => throw new JSException("rejected");
            await Dispatch(x, new Increment()); // its send fails: inert from here on
        }

        await Dispatch(x, new SetLabel("latest"));
        var state = x.Store.State;
        x.Logs.Clear();

        string[] messages =
        [
            Jump("JUMP_TO_ACTION", 2),
            """{"type":"DISPATCH","payload":{"type":"RESET"}}""",
            """{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[{"state":{"counter":{"Value":9}}}]}}}""",
        ];
        for (var seq = 0; seq < messages.Length; seq++)
        {
            await x.Middleware.OnMessageAsync(messages[seq], seq, false);
            await x.Store.WhenIdleAsync(Ct);
        }

        // A large one is taken, so ducky.js no longer keeps it, but never read while inert.
        var reference = new FakeJsStreamReference(Encoding.UTF8.GetBytes(messages[2]));
        x.OnTake = _ => reference;
        await x.Middleware.OnMessageAsync(null, 3, tooLarge: true);
        await x.Store.WhenIdleAsync(Ct);

        x.Store.State.ShouldBeSameAs(state);
        x.Logs.GetSnapshot().Where(static r => r.Id.Id == 2065).Select(static r => (r.Level, r.Message)).ShouldBe(
        [
            (LogLevel.Debug, "Redux DevTools message 0 ignored (type 'JUMP_TO_ACTION', too large: False)"),
            (LogLevel.Debug, "Redux DevTools message 1 ignored (type 'RESET', too large: False)"),
            (LogLevel.Debug, "Redux DevTools message 2 ignored (type 'IMPORT_STATE', too large: False)"),
            (LogLevel.Debug, "Redux DevTools message 3 ignored (type '(null)', too large: True)"),
        ]);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2066);
        x.Js.Calls.Where(static call => call.Identifier == "devtoolsTakeMessage").ShouldHaveSingleItem().Args.ShouldBe([x.StoreId, 3L]);
        reference.Opened.ShouldBeFalse();
        reference.Disposed.ShouldBeTrue();
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
    }

    [Fact]
    public async Task DevTools_JumpMessage_StateStrippedInline()
    {
        // INV-23, §11.8: ducky.js forwards a jump as {type, payload: {type, actionId, index}}, without the extension's state,
        // and inline whatever the budget. With a 2-byte budget and a state far above it, the jump still arrives inline and
        // restores the typed entry; nothing is pulled.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(), blazor: static o => o.InlinePayloadBytes = 2);
        await x.InitializeAsync();
        await Dispatch(x, new SetLabel(new string('x', 64 * 1024))); // 2
        var label = x.Store.State.Get<Label>();
        await Dispatch(x, new SetLabel("latest")); // 3
        ((string)x.Connect[2]!).ShouldContain("\"maxInlineBytes\":2");

        await Send(x, """{"type":"DISPATCH","payload":{"type":"JUMP_TO_ACTION","actionId":2,"index":2}}""");

        x.Store.State.Get<Label>().ShouldBeSameAs(label);
        x.Identifiers.ShouldNotContain("devtoolsTakeMessage");
        x.EventIds(LogLevel.Debug).ShouldNotContain(2065);
    }

    // INV-23, §11.8: a message above InlinePayloadBytes arrives as (null, seq, tooLarge) and is pulled once through
    // devtoolsTakeMessage(id, seq) by stream, read with maxAllowedSize MaxPayloadBytes and always disposed. One at
    // MaxPayloadBytes is read; Length 1 (already taken or pruned) is ignored with a Debug log; one byte above MaxPayloadBytes
    // is ignored with Warning 2023, decided from Length before the stream is opened; a rejected or interrupted pull, and a
    // disposal on a dropped circuit, never throw.
    [Theory]
    [InlineData("read")]
    [InlineData("taken")]
    [InlineData("over")]
    [InlineData("rejected")]
    [InlineData("interrupted")]
    [InlineData("dispose-disconnected")]
    [InlineData("dispose-cancelled")]
    public async Task DevTools_LargeImport_ReadViaStream(string pull)
    {
        const int MaxPayloadBytes = 64 * 1024;
        const string Import = """{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[{"state":{"counter":{"Value":5},"label":{"Text":"imported"}}}]}}}""";
        await using var x = new DevToolsHarness(static d => d.UseDevTools(), blazor: static o => o.MaxPayloadBytes = MaxPayloadBytes);
        await x.InitializeAsync();
        await Dispatch(x, new Increment());
        await Send(x, Jump("JUMP_TO_ACTION", 1)); // time travel, which the import releases
        var state = x.Store.State;
        var reference = new FakeJsStreamReference(pull == "taken" ? new byte[1] : Encoding.UTF8.GetBytes(Import.PadRight(MaxPayloadBytes + (pull == "over" ? 1 : 0))))
        {
            OnDispose = pull switch
            {
                "dispose-disconnected" => new JSDisconnectedException("circuit gone"),
                "dispose-cancelled" => new TaskCanceledException("interop timed out"),
                _ => null,
            },
        };
        x.OnTake = pull switch
        {
            "rejected" => static _ => Task.FromException<object?>(new JSException("rejected")),
            "interrupted" => static _ => Task.FromException<object?>(new JSDisconnectedException("circuit gone")),
            _ => _ => reference,
        };
        x.Logs.Clear();

        await x.Middleware.OnMessageAsync(null, 5, tooLarge: true);
        await x.Store.WhenIdleAsync(Ct);

        x.Js.Calls.Where(static call => call.Identifier == "devtoolsTakeMessage").ShouldHaveSingleItem().Args.ShouldBe([x.StoreId, 5L]);
        x.Logs.GetSnapshot().Where(static r => r.Level >= LogLevel.Warning).Select(static r => (r.Id.Id, r.Message)).ShouldBe(pull == "over"
            ? [(2023, $"Redux DevTools message 5 is {(MaxPayloadBytes + 1).ToString(CultureInfo.InvariantCulture)} bytes, above MaxPayloadBytes ({MaxPayloadBytes.ToString(CultureInfo.InvariantCulture)}); ignored")]
            : []);
        reference.Opened.ShouldBe(pull is "read" or "dispose-disconnected" or "dispose-cancelled");
        reference.Disposed.ShouldBe(pull is not ("rejected" or "interrupted"));
        x.Middleware.Sends.ShouldBe(2); // an import's restore is never sent
        if (pull is "read" or "dispose-disconnected" or "dispose-cancelled")
        {
            x.Store.State.Get<Counter>().Value.ShouldBe(5);
            x.Store.State.Get<Label>().Text.ShouldBe("imported");
            x.EventIds(LogLevel.Debug).ShouldNotContain(2065);
            x.EventIds(LogLevel.Debug).Contains(2000).ShouldBe(pull != "read"); // a disposal the circuit interrupted, logged
            (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        }
        else
        {
            x.Store.State.ShouldBeSameAs(state);
            x.Logs.GetSnapshot().Where(static r => r.Id.Id == 2065).Select(static r => (r.Level, r.Message)).ShouldBe(
                [(LogLevel.Debug, "Redux DevTools message 5 ignored (type '(null)', too large: True)")]);
            (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed); // still time travelling
        }
    }

    // §11.8: ducky.js forwards each message without waiting for the last one to settle, and a large one is handled only once
    // its pull returns. Messages are handled in seq order all the same: a jump that arrives while an import is still being
    // pulled is applied after it, so the state and the veto are the jump's. A pull that faults (no JS exception) faults its
    // own delivery only; the next message is still handled.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DevTools_InlineJumpDuringLargeImportPull_HandledInSeqOrder(bool faulted)
    {
        const string Import = """{"type":"DISPATCH","payload":{"type":"IMPORT_STATE","nextLiftedState":{"computedStates":[{"state":{"counter":{"Value":5}}}]}}}""";
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync(); // 0 @@INIT, 1 StoreInitialized
        var first = x.Store.State.Get<Counter>();
        await Dispatch(x, new Increment()); // 2
        var take = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.OnTake = _ => take.Task;

        var import = x.Middleware.OnMessageAsync(null, 1, tooLarge: true);
        var jump = x.Middleware.OnMessageAsync(Jump("JUMP_TO_ACTION", 1), 2, false);
        jump.IsCompleted.ShouldBeFalse(); // behind the import
        if (faulted)
        {
            take.SetException(new InvalidOperationException("faulted"));
        }
        else
        {
            take.SetResult(new FakeJsStreamReference(Encoding.UTF8.GetBytes(Import)));
        }

        await jump.WaitAsync(_bound, Ct);
        (await Record.ExceptionAsync(() => import.WaitAsync(_bound, Ct)))?.Message.ShouldBe(faulted ? "faulted" : null);
        import.IsFaulted.ShouldBe(faulted);
        await x.Store.WhenIdleAsync(Ct);

        x.Store.State.Get<Counter>().ShouldBeSameAs(first);
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);
    }

    [Fact]
    public async Task DevTools_JumpConcurrentWithSend_RestoresMatchingEntry_Deterministic()
    {
        // INV-28, §11.8: the fake runtime delivers each jump re-entrantly from inside devtoolsSend, on the drainer, mid-send.
        // The entry being sent is already published, and a jump reads one published array: a jump to the entry before
        // restores it, and a jump to the entry in flight finds it as the latest, so time travel releases.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        Dictionary<long, string> jumps = [];
        var sends = 0L;
        x.OnSend = _ =>
        {
            if (jumps.Remove(++sends, out var jump))
            {
                Settled(x.Middleware.OnMessageAsync(jump, sends, false));
            }

            return true;
        };
        await x.InitializeAsync(); // 0 @@INIT, 1 StoreInitialized
        await Dispatch(x, new Increment()); // 2
        await Dispatch(x, new Increment()); // 3
        var third = x.Store.State.Get<Counter>();

        jumps[4] = Jump("JUMP_TO_ACTION", 3);
        await Dispatch(x, new Increment()); // 4, the jump to 3 delivered inside its send
        x.Store.State.Get<Counter>().ShouldBeSameAs(third);
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Vetoed);

        jumps[5] = """{"type":"DISPATCH","payload":{"type":"JUMP_TO_STATE","index":5}}""";
        x.Store.Restore(new Dictionary<string, object> { ["label"] = new Label("restored") }, Origin.Hydration); // 5
        await x.Store.WhenIdleAsync(Ct);
        x.Store.State.Get<Counter>().ShouldBeSameAs(third);
        x.Store.State.Get<Label>().Text.ShouldBe("restored");
        (await x.Store.DispatchAsync(new Increment()).WaitAsync(_bound, Ct)).ShouldBe(DispatchResult.Reduced);
        x.Store.State.Get<Counter>().Value.ShouldBe(3);
        x.Middleware.Sends.ShouldBe(6);
        jumps.ShouldBeEmpty();

        static void Settled(Task delivered) => delivered.IsCompletedSuccessfully.ShouldBeTrue(); // an inline message settles at once
    }

    private static async Task Dispatch(DevToolsHarness x, object action)
    {
        x.Store.Dispatch(action);
        await x.Store.WhenIdleAsync(Ct);
    }

    private static string Jump(string type, long actionId) =>
        $$$"""{"type":"DISPATCH","payload":{"type":"{{{type}}}","actionId":{{{actionId}}}}}""";

    private static Task Message(DevToolsHarness x, string json) => x.Middleware.OnMessageAsync(json, 1, false);

    private static async Task Send(DevToolsHarness x, string json)
    {
        await Message(x, json);
        await x.Store.WhenIdleAsync(Ct);
    }
}
