using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DevTools;

// SPEC §11.8 outbound (DT-01, DT-02), INV-23, INV-28: UseDevTools registers DevToolsMiddleware once and composes its
// options; init reads the gate synchronously (probing once when Unknown), connects with a pre-serialized config bounded
// by HydrationTimeout, goes inert when the extension is absent, the connect fails or devtoolsSend reports an unknown id,
// and sends a sanitized @@INIT; AfterReduce filters, builds the FSA (library actions and failures projected through
// BlazorWireContext, the rest through the app's resolver by runtime type, null => meta.unserializable), the state per
// slice by its declared type (@ducky/* through BlazorWireContext), both sanitized, and the trace through DescribeFrame.
public sealed class DevToolsTests
{
    private const string Prefix = "Ducky.Blazor.Tests.DevTools.";
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task DevTools_OptionsReachExtension()
    {
        // Every configure of every call applies, in call order, to one DevToolsOptions; the middleware is registered once.
        await using var x = new DevToolsHarness(static d => d
            .UseDevTools(static o =>
            {
                o.Name = "Shop";
                o.MaxAge = 7;
                o.Trace = true; // the .NET side supplies the stack: the extension's own trace stays off
            })
            .UseDevTools(static o => o.Name = "Cart")
            .UseDevTools(), blazor: static o => o.InlinePayloadBytes = 1234);
        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.Identifiers.ShouldBe(["import", "devtoolsConnect", "devtoolsInit", "devtoolsSend", "devtoolsSend"]);
        var connect = x.Connect;
        connect.Length.ShouldBe(4);
        connect[0].ShouldBe(x.StoreId);
        x.StoreId.ShouldMatch("^[0-9a-f]{32}$");
        connect[1].ShouldBeOfType<DotNetObjectReference<DevToolsMiddleware>>();
        // The inline budget rides in configJson: devtoolsConnect has no budget argument and ducky.js holds no default (§11.8, §11.9).
        connect[2].ShouldBe("""{"name":"Cart","maxAge":7,"trace":false,"maxInlineBytes":1234}""");
        connect[3].ShouldBe((long)TimeSpan.FromMinutes(10).TotalMilliseconds); // the prune cap of §11.7
        x.Inits.ShouldHaveSingleItem().Id.ShouldBe(x.StoreId);
        x.Sends.ShouldAllBe(sent => sent.Id == x.StoreId);
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", "Ducky.Blazor.Tests.Core.Increment"]);

        // The defaults, from a store whose UseDevTools adds Ducky.Blazor itself; another store has another id.
        var js = new FakeJsRuntime();
        js.Respond = (identifier, _) => identifier == "import" ? js : identifier == "devtoolsConnect";
        var services = new ServiceCollection().AddSingleton<IJSRuntime>(js);
        services.AddDucky(static d => d.AddSlice<CounterSlice>().UseDevTools());
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IStore>().InitializeAsync(Ct);
        var defaults = js.Calls.Single(static call => call.Identifier == "devtoolsConnect").Args;
        defaults[2].ShouldBe("""{"name":"Ducky","maxAge":50,"trace":false,"maxInlineBytes":16384}""");
        defaults[0].ShouldBe(provider.GetRequiredService<PersistenceSlice>().Id);
        defaults[0].ShouldNotBe(x.StoreId);
        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.UseDevTools(null!)).ParamName.ShouldBe("builder");
    }

    [Fact]
    public async Task DevTools_SanitizerAppliedToInitAndEverySend()
    {
        List<(string Type, string? Payload)> actions = [];
        List<string> keys = [];
        await using var x = new DevToolsHarness(d => d.UseDevTools(o =>
        {
            o.ActionSanitizer = (type, payload) =>
            {
                actions.Add((type, payload?.ToJsonString()));
                return new JsonObject { ["redacted"] = type };
            };
            o.StateSanitizer = (key, state) =>
            {
                keys.Add(key);
                return key == "label" ? JsonValue.Create("***") : state;
            };
        }));
        await x.InitializeAsync();
        x.Store.Dispatch(new SetLabel("secret"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        // @@INIT: every slice by its declared type, then sanitized; the library slice through BlazorWireContext (the app's
        // resolver doesn't know PersistenceState), sanitized too.
        var init = x.Inits.ShouldHaveSingleItem().State;
        init.ToJsonString().ShouldBe("""{"counter":{"Value":0},"label":"***","@ducky/persistence":{"status":"Hydrated"}}""");

        // Every send: the payload and every slice sanitized, the library slice present.
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", Prefix + "SetLabel", "Ducky.Blazor.Tests.Core.Increment"]);
        x.Sends.Select(static sent => sent.Action.ToJsonString()).ShouldBe(
        [
            """{"type":"Ducky.StoreInitialized","payload":{"redacted":"Ducky.StoreInitialized"}}""",
            """{"type":"Ducky.Blazor.Tests.DevTools.SetLabel","payload":{"redacted":"Ducky.Blazor.Tests.DevTools.SetLabel"}}""",
            """{"type":"Ducky.Blazor.Tests.Core.Increment","payload":{"redacted":"Ducky.Blazor.Tests.Core.Increment"}}""",
        ]);
        x.Sends.Select(static sent => sent.State.ToJsonString()).ShouldBe(
        [
            """{"counter":{"Value":0},"label":"***","@ducky/persistence":{"status":"Hydrated"}}""",
            """{"counter":{"Value":0},"label":"***","@ducky/persistence":{"status":"Hydrated"}}""",
            """{"counter":{"Value":1},"label":"***","@ducky/persistence":{"status":"Hydrated"}}""",
        ]);

        // The sanitizers saw the unsanitized values: each action type with its payload, each slice key once per payload.
        actions.ShouldBe([("Ducky.StoreInitialized", "{}"), (Prefix + "SetLabel", """{"Text":"secret"}"""), ("Ducky.Blazor.Tests.Core.Increment", "{}")]);
        keys.ShouldBe([.. Enumerable.Repeat<string[]>(["counter", "label", "@ducky/persistence"], 4).SelectMany(static k => k)]);
        x.Js.Calls.SelectMany(static call => call.Args).OfType<string>().ShouldAllBe(static arg => !arg.Contains("secret"));
    }

    [Fact]
    public async Task DevTools_SanitizerReturningAttachedNode_IsSentAsCopy()
    {
        // A JsonNode has one parent: a sanitizer that unwraps a child of its input, or returns one cached node every time,
        // hands back a node that is (or, after its first use, becomes) attached elsewhere. It is copied, never re-parented.
        var redacted = JsonValue.Create("***");
        await using var x = new DevToolsHarness(d => d.UseDevTools(o =>
        {
            o.ActionSanitizer = static (_, payload) => payload?["Text"];
            o.StateSanitizer = (key, state) => key == "label" ? redacted : state;
        }));
        await x.InitializeAsync();
        x.Store.Dispatch(new SetLabel("secret"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.Inits.ShouldHaveSingleItem().State["label"]!.ToJsonString().ShouldBe("\"***\"");
        x.Sends.Select(static sent => sent.Action.ToJsonString()).ShouldBe(
        [
            """{"type":"Ducky.StoreInitialized","payload":null}""",
            """{"type":"Ducky.Blazor.Tests.DevTools.SetLabel","payload":"secret"}""",
            """{"type":"Ducky.Blazor.Tests.Core.Increment","payload":null}""",
        ]);
        x.Sends.Select(static sent => sent.State["label"]!.ToJsonString()).ShouldBe(["\"***\"", "\"***\"", "\"***\""]);
        x.EventIds(LogLevel.Warning).ShouldBeEmpty();
        x.EventIds(LogLevel.Error).ShouldBeEmpty();
    }

    [Fact]
    public async Task DevTools_FilterExcludesAction()
    {
        List<string> offered = [];
        await using var x = new DevToolsHarness(d => d.UseDevTools(o => o.Filter = context =>
        {
            offered.Add(context.ActionType);
            return context.Action is not Increment;
        }));
        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        x.Store.Dispatch(new SetLabel("a"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        // A filtered action is not sent and doesn't move the DevTools-local counter; its change shows in the next state.
        offered.ShouldBe(["Ducky.StoreInitialized", "Ducky.Blazor.Tests.Core.Increment", Prefix + "SetLabel", "Ducky.Blazor.Tests.Core.Increment"]);
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", Prefix + "SetLabel"]);
        x.Sends[1].State["counter"]!.ToJsonString().ShouldBe("""{"Value":1}""");
        x.Middleware.Sends.ShouldBe(2);
        x.Store.State.Get<Counter>().Value.ShouldBe(2);
    }

    [Fact]
    public async Task DevTools_ExtensionAbsent_Inert()
    {
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        x.OnConnect = static _ => false;
        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);
        var reference = (DotNetObjectReference<DevToolsMiddleware>)x.Connect[1]!;

        // No @@INIT, no send, one Warning; the store itself works.
        x.Identifiers.ShouldBe(["import", "devtoolsConnect"]);
        x.EventIds(LogLevel.Warning).ShouldBe([2061]);
        x.Store.State.Get<Counter>().Value.ShouldBe(1);

        // Nothing to disconnect at dispose; the reference is released.
        await x.DisposeAsync();
        x.Identifiers.ShouldBe(["import", "devtoolsConnect"]);
        Should.Throw<ObjectDisposedException>(() => reference.Value);
    }

    [Fact]
    public async Task DevTools_SendReportsUnknownId_GoesInert()
    {
        // ducky.js answers false for an id it doesn't know (pruned, routine past the 10-minute cap): DevTools stops sending,
        // with one Debug log naming it and no Warning (§11.7 "Delivery failures").
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        x.OnSend = static args => !((string)args[1]!).Contains("SetLabel", StringComparison.Ordinal);
        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        x.Store.Dispatch(new SetLabel("a"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", "Ducky.Blazor.Tests.Core.Increment", Prefix + "SetLabel"]);
        x.EventIds(LogLevel.Debug).Count(static id => id == 2062).ShouldBe(1);
        x.EventIds(LogLevel.Warning).ShouldBeEmpty();
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2062).Message.ShouldContain(x.StoreId);

        // The id is gone from ducky.js: nothing to disconnect.
        await x.DisposeAsync();
        x.Identifiers.ShouldNotContain("devtoolsDisconnect");

        // Real sends answer later, so several are in flight when the id is pruned: still one Debug log, and no disconnect.
        // No context is captured in tests and the answer doesn't force asynchronous continuations, so both run inside SetResult.
        var answer = new TaskCompletionSource<object?>();
        await using var pending = new DevToolsHarness(static d => d.UseDevTools());
        pending.OnSend = args => ((string)args[1]!).Contains("StoreInitialized", StringComparison.Ordinal) ? true : answer.Task;
        await pending.InitializeAsync();
        pending.Store.Dispatch(new Increment());
        pending.Store.Dispatch(new Increment());
        await pending.Store.WhenIdleAsync(Ct);
        pending.Middleware.Sends.ShouldBe(3);
        answer.SetResult(false);

        pending.EventIds(LogLevel.Debug).Count(static id => id == 2062).ShouldBe(1);
        pending.EventIds(LogLevel.Warning).ShouldBeEmpty();
        await pending.DisposeAsync();
        pending.Identifiers.ShouldNotContain("devtoolsDisconnect");
    }

    [Fact]
    public async Task DevTools_PolymorphicState_SentViaDeclaredType()
    {
        // The outbound half of DevTools_PolymorphicState_SerializedAndImportedViaDeclaredType (M8-03b adds IMPORT_STATE):
        // the state goes through its declared abstract type, so the discriminator travels; the runtime type would drop it.
        await using var x = new DevToolsHarness(static d => d.AddSlice<BirdSlice>().UseDevTools());
        await x.InitializeAsync();
        x.Store.Dispatch(new Hatch("Ducky"));
        await x.Store.WhenIdleAsync(Ct);

        x.Inits.ShouldHaveSingleItem().State["bird"]!.ToJsonString().ShouldBe("""{"$type":"goose","Honks":3}""");
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", Prefix + "Hatch"]);
        x.Sends.Select(static sent => sent.State["bird"]!.ToJsonString()).ShouldBe(
            ["""{"$type":"goose","Honks":3}""", """{"$type":"duckling","Name":"Ducky"}"""]);
    }

    [Fact]
    public async Task DevTools_GateUnknown_ProbesAndDoesNotDelayInit()
    {
        // A store first touched by non-component code, outside the browser: the gate is Unknown, so DevTools probes once,
        // synchronously, and never waits for a component registration. The connect never answers: its HydrationTimeout
        // bound ends it with a Warning, well before InitTimeout, and DevTools is inert.
        var timeout = TimeSpan.FromSeconds(2);
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var x = new DevToolsHarness(static d => d.UseDevTools(), browser: false, blazor: o => o.HydrationTimeout = timeout);
        x.OnConnect = _ => held.Task;
        var init = x.InitializeAsync();

        x.Identifiers.ShouldBe(["import", "devtoolsConnect"]); // the probe's import, then the connect on that module
        x.Time.Advance(timeout - TimeSpan.FromTicks(1));
        await Task.Yield();
        init.IsCompleted.ShouldBeFalse();
        x.Time.Advance(TimeSpan.FromTicks(1));
        await init.WaitAsync(_bound, Ct);

        x.EventIds(LogLevel.Warning).ShouldBe([2060]);
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2060).Message.ShouldContain("00:00:02");
        x.EventIds(LogLevel.Error).ShouldBeEmpty(); // no StoreInitAborted
        x.Store.Dispatch(new Increment());
        held.SetResult(true); // a late answer changes nothing
        await x.Store.WhenIdleAsync(Ct);
        x.Store.State.Get<Counter>().Value.ShouldBe(1);
        x.Identifiers.ShouldBe(["import", "devtoolsConnect"]);

        // ducky.js may have registered the id after .NET gave up: the disposal disconnects it.
        await x.DisposeAsync();
        x.Identifiers.ShouldBe(["import", "devtoolsConnect", "devtoolsDisconnect"]);

        // A disposal during the connect abandons it: no timeout Warning, and the id is disconnected.
        var never = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var disposed = new DevToolsHarness(static d => d.UseDevTools());
        disposed.OnConnect = _ => never.Task;
        var abandoned = disposed.InitializeAsync();
        await disposed.DisposeAsync();
        await abandoned.WaitAsync(_bound, Ct); // the disposal ends init
        disposed.EventIds(LogLevel.Warning).ShouldBeEmpty();
        disposed.Identifiers.ShouldBe(["import", "devtoolsConnect", "devtoolsDisconnect"]);
    }

    [Fact]
    public async Task DevTools_NonInteractive_NeverConnects()
    {
        // The probe finds no interactive runtime (prerender, static SSR): DevTools is inert, silently, and init doesn't wait.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(), browser: false);
        x.OnImport = static () => throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.Identifiers.ShouldBe(["import"]);
        x.EventIds(LogLevel.Warning).ShouldBeEmpty();
        await x.DisposeAsync();
        x.Identifiers.ShouldBe(["import"]);
    }

    [Theory]
    [InlineData("connect-rejected")]
    [InlineData("init-rejected")]
    [InlineData("init-interrupted")]
    public async Task DevTools_ConnectOrInitFails_Inert(string failure)
    {
        // A failed connect is a 2060 (init-time, bounded by HydrationTimeout); a failed @@INIT, issued on the drainer
        // like a send, is a 2063. Either way nothing is sent.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        if (failure == "connect-rejected")
        {
            x.OnConnect = static _ => throw new JSException("rejected");
        }
        else
        {
            x.OnInit = failure == "init-rejected" ? static _ => throw new JSException("rejected") : static _ => throw new JSDisconnectedException("gone");
        }

        await x.InitializeAsync();
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.Identifiers.ShouldNotContain("devtoolsSend");
        x.Middleware.Sends.ShouldBe(0);
        var id = failure == "connect-rejected" ? 2060 : 2063;
        x.EventIds(LogLevel.Warning).ShouldBe([id]);
        var logged = x.Logs.GetSnapshot().Single(record => record.Id.Id == id).Exception;
        if (failure == "init-interrupted")
        {
            logged.ShouldBeNull(); // interrupted: the bridge logged it at Debug
            x.EventIds(LogLevel.Debug).ShouldContain(2000);
        }
        else
        {
            logged.ShouldBeOfType<JSException>().Message.ShouldBe("rejected");
        }

        x.Identifiers.Count(static identifier => identifier == "devtoolsInit").ShouldBe(failure == "connect-rejected" ? 0 : 1);

        // devtoolsConnect was issued: ducky.js may hold the id (and the reference), so the disposal disconnects it.
        await x.DisposeAsync();
        x.Identifiers[^1].ShouldBe("devtoolsDisconnect");
    }

    [Fact]
    public async Task DevTools_InitFailsLater_GoesInert()
    {
        // A real @@INIT answers later: StoreInitialized is already sent behind it. Its failure stops every later send,
        // with one Warning.
        var answer = new TaskCompletionSource<object?>();
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        x.OnInit = _ => answer.Task;
        await x.InitializeAsync();
        await x.Store.WhenIdleAsync(Ct);
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized"]);
        answer.SetException(new JSException("rejected"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.SentTypes.ShouldBe(["Ducky.StoreInitialized"]);
        x.Middleware.Sends.ShouldBe(1);
        x.EventIds(LogLevel.Warning).ShouldBe([2063]);
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2063).Exception.ShouldBeOfType<JSException>();
        x.Inits.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DevTools_RestoreWhileInitIsSent_IsSentAfterInit()
    {
        // A restore reduced while @@INIT is on its way (here: issued from inside devtoolsInit) is either in @@INIT or sent
        // as its own action: @@INIT and the switch to sending happen on the drainer, so no reduce falls between them.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        x.OnInit = _ =>
        {
            x.Store.Restore(new Dictionary<string, object> { ["label"] = new Label("restored") }, Origin.Hydration);
            return null;
        };
        await x.InitializeAsync();
        await x.Store.WhenIdleAsync(Ct);

        x.Identifiers.ShouldBe(["import", "devtoolsConnect", "devtoolsInit", "devtoolsSend", "devtoolsSend"]);
        x.Inits.ShouldHaveSingleItem().State["label"]!.ToJsonString().ShouldBe("""{"Text":""}""");
        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", "@ducky/restore"]);
        x.Sends[0].State["label"]!.ToJsonString().ShouldBe("""{"Text":""}""");
        x.Sends[1].State["label"]!.ToJsonString().ShouldBe("""{"Text":"restored"}""");
        x.Middleware.Sends.ShouldBe(2);
    }

    [Fact]
    public async Task DevTools_RestoreDuringInit_InitCarriesStateBeforeIt()
    {
        // Connected while another middleware's init is still pending: the restore reduced meanwhile is the first action
        // DevTools sees, so @@INIT carries the state before it, then the restore and StoreInitialized are sent, in order.
        var held = new TaskCompletionSource<object?>();
        await using var x = new DevToolsHarness(static d => d.UseDevTools().Use<HoldInit>());
        x.OnHold = () => held.Task;
        var init = x.InitializeAsync();
        x.Identifiers.ShouldBe(["import", "devtoolsConnect", "hold"]);
        x.Store.Restore(new Dictionary<string, object> { ["label"] = new Label("restored") }, Origin.Hydration);
        held.SetResult(null);
        await init.WaitAsync(_bound, Ct);
        await x.Store.WhenIdleAsync(Ct);

        x.Identifiers.ShouldBe(["import", "devtoolsConnect", "hold", "devtoolsInit", "devtoolsSend", "devtoolsSend"]);
        x.Inits.ShouldHaveSingleItem().State["label"]!.ToJsonString().ShouldBe("""{"Text":""}""");
        x.SentTypes.ShouldBe(["@ducky/restore", "Ducky.StoreInitialized"]);
        x.Sends[0].State["label"]!.ToJsonString().ShouldBe("""{"Text":"restored"}""");
        x.Sends[1].State["label"]!.ToJsonString().ShouldBe("""{"Text":"restored"}""");
    }

    [Fact]
    public async Task DevTools_FailureAction_ShowsTypeAndMessage()
    {
        await using var x = new DevToolsHarness(static d => d.AddSlice<ExplodingSlice>().AddEffect<ExplodingEffect>().UseDevTools());
        await x.InitializeAsync();
        x.Store.Dispatch(new Explode());
        x.Store.Dispatch(new Blast());
        await x.Store.WhenIdleAsync(Ct);

        // The exception's type name and message, with the slice key or the effect type; the exception itself never crosses.
        x.Sends.Single(static sent => (string)sent.Action["type"]! == "Ducky.ReducerFailed").Action.ToJsonString()
            .ShouldBe("""{"type":"Ducky.ReducerFailed","payload":{"type":"InvalidOperationException","message":"reducer boom","sliceKey":"exploding"}}""");
        x.Sends.Single(static sent => (string)sent.Action["type"]! == "Ducky.EffectFailed").Action.ToJsonString()
            .ShouldBe("""{"type":"Ducky.EffectFailed","payload":{"type":"ArgumentException","message":"effect boom","effectType":"Ducky.Blazor.Tests.DevTools.ExplodingEffect"}}""");
    }

    [Fact]
    public async Task DevTools_LibraryActions_ProjectedThroughWireContext()
    {
        // The app's resolver knows none of Ducky's or Ducky.Blazor's own actions: BlazorWireContext serializes them.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        x.Store.Dispatch(new HydrationCompleted(true));
        x.Store.Dispatch(new HydrationFailed("TimeoutException", "slow"));
        x.Store.Dispatch(new PersistenceFailed("label", "IOException", "full"));
        x.Store.Dispatch(new ClearPersistedState());
        await x.Store.WhenIdleAsync(Ct);

        x.Sends.Select(static sent => sent.Action.ToJsonString()).ShouldBe(
        [
            """{"type":"Ducky.StoreInitialized","payload":{}}""",
            """{"type":"Ducky.Blazor.HydrationCompleted","payload":{"stateRestored":true}}""",
            """{"type":"Ducky.Blazor.HydrationFailed","payload":{"errorType":"TimeoutException","message":"slow"}}""",
            """{"type":"Ducky.Blazor.PersistenceFailed","payload":{"sliceKey":"label","errorType":"IOException","message":"full"}}""",
            """{"type":"Ducky.Blazor.ClearPersistedState","payload":{}}""",
        ]);
    }

    [Fact]
    public async Task DevTools_UnserializableAction_NullPayloadWithMeta()
    {
        // No type info for the action's runtime type: the payload is null and meta.unserializable is set, whatever the
        // sanitizer returns for it; a serializable action carries no meta.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(static o => o.ActionSanitizer = static (type, payload) =>
            payload ?? new JsonObject { ["degraded"] = type }));
        await using var plain = new DevToolsHarness(static d => d.UseDevTools());
        foreach (var store in new[] { x, plain })
        {
            await store.InitializeAsync();
            store.Store.Dispatch(new Opaque(1));
            store.Store.Dispatch(new SetLabel("a"));
            await store.Store.WhenIdleAsync(Ct);
        }

        x.Sends.Skip(1).Select(static sent => sent.Action.ToJsonString()).ShouldBe(
        [
            """{"type":"Ducky.Blazor.Tests.DevTools.Opaque","payload":{"degraded":"Ducky.Blazor.Tests.DevTools.Opaque"},"meta":{"unserializable":true}}""",
            """{"type":"Ducky.Blazor.Tests.DevTools.SetLabel","payload":{"Text":"a"}}""",
        ]);
        plain.Sends[1].Action.ToJsonString().ShouldBe("""{"type":"Ducky.Blazor.Tests.DevTools.Opaque","payload":null,"meta":{"unserializable":true}}""");
    }

    [Fact]
    public async Task DevTools_Trace_UsesDiagnosticMethodInfo()
    {
        // Frames captured synchronously in AfterReduce, each described by DiagnosticMethodInfo, up to TraceLimit.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(static o => (o.Trace, o.TraceLimit) = (true, 500)));
        await using var limited = new DevToolsHarness(static d => d.UseDevTools(static o => (o.Trace, o.TraceLimit) = (true, 2)));
        foreach (var store in new[] { x, limited })
        {
            await store.InitializeAsync();
            DispatchIncrement(store.Store);
            await store.Store.WhenIdleAsync(Ct);
        }

        var lines = ((string)x.Sends[^1].Action["stack"]!).Split('\n');
        lines[0].ShouldBe("    at Ducky.Blazor.DevToolsMiddleware.AfterReduce");
        lines.ShouldContain($"    at {typeof(DevToolsTests).FullName}.{nameof(DispatchIncrement)}");
        lines.ShouldAllBe(static line => line.StartsWith("    at ", StringComparison.Ordinal) && line != "    at <unknown>");
        lines.Length.ShouldBeLessThan(500);

        // TraceLimit frames at most.
        ((string)limited.Sends[^1].Action["stack"]!).Split('\n').Length.ShouldBe(2);

        // Off by default: no stack.
        await using var off = new DevToolsHarness(static d => d.UseDevTools());
        await off.InitializeAsync();
        off.Sends.ShouldHaveSingleItem().Action.ContainsKey("stack").ShouldBeFalse();
    }

    [Fact]
    public async Task DevTools_Trace_TrimmedFrame_RendersUnknown()
    {
        // Metadata trimmed away (NativeAOT): DiagnosticMethodInfo is null, so each frame renders as <unknown>.
        await using var x = new DevToolsHarness(static d => d.UseDevTools(static o =>
        {
            o.Trace = true;
            o.DescribeFrame = static _ => null;
        }));
        await x.InitializeAsync();

        ((string)x.Sends.ShouldHaveSingleItem().Action["stack"]!).ShouldBe(string.Join("\n", Enumerable.Repeat("    at <unknown>", 10)));
    }

    [Fact]
    public async Task DevTools_Dispose_DisconnectsAndReleasesReference()
    {
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        await x.InitializeAsync();
        var reference = (DotNetObjectReference<DevToolsMiddleware>)x.Connect[1]!;
        var id = x.StoreId;
        await x.DisposeAsync();

        x.Js.Calls[^1].Identifier.ShouldBe("devtoolsDisconnect");
        x.Js.Calls[^1].Args.ShouldBe([id]);
        Should.Throw<ObjectDisposedException>(() => reference.Value);
        x.Js.Disposed.ShouldBe(1); // its module

        // A disconnect that answers later is awaited: the reference stays alive until ducky.js dropped the connection.
        var answer = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var slow = new DevToolsHarness(static d => d.UseDevTools());
        slow.OnDisconnect = _ => answer.Task;
        await slow.InitializeAsync();
        var held = (DotNetObjectReference<DevToolsMiddleware>)slow.Connect[1]!;
        var disposing = slow.DisposeAsync().AsTask();
        await Task.Yield();
        disposing.IsCompleted.ShouldBeFalse();
        held.Value.ShouldNotBeNull();
        answer.SetResult(null);
        await disposing.WaitAsync(_bound, Ct);
        Should.Throw<ObjectDisposedException>(() => held.Value);

        // A rejected disconnect is a Warning; the disposal still completes and releases the reference.
        await using var rejected = new DevToolsHarness(static d => d.UseDevTools());
        rejected.OnDisconnect = static _ => throw new JSException("gone");
        await rejected.InitializeAsync();
        var released = (DotNetObjectReference<DevToolsMiddleware>)rejected.Connect[1]!;
        await rejected.DisposeAsync();
        rejected.EventIds(LogLevel.Warning).ShouldBe([2064]);
        Should.Throw<ObjectDisposedException>(() => released.Value);
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("interrupted")]
    public async Task DevTools_SendFails_GoesInert(string failure)
    {
        // The extension may or may not hold a failed send, so the DevTools-local counter (+1 per actual devtoolsSend,
        // §11.8) could no longer match its index: DevTools stops sending, with one Warning, and still disconnects.
        await using var x = new DevToolsHarness(static d => d.UseDevTools());
        x.OnSend = args => !((string)args[1]!).Contains("SetLabel", StringComparison.Ordinal)
            ? true
            : failure == "rejected" ? throw new JSException("rejected") : throw new JSDisconnectedException("gone");
        await x.InitializeAsync();
        x.Store.Dispatch(new SetLabel("a"));
        x.Store.Dispatch(new SetLabel("b"));
        x.Store.Dispatch(new Increment());
        await x.Store.WhenIdleAsync(Ct);

        x.SentTypes.ShouldBe(["Ducky.StoreInitialized", Prefix + "SetLabel"]);
        x.Middleware.Sends.ShouldBe(2);
        x.EventIds(LogLevel.Warning).ShouldBe([2063]);
        var logged = x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2063);
        logged.Message.ShouldContain("DevTools is inert for this store");
        if (failure == "rejected")
        {
            logged.Exception.ShouldBeOfType<JSException>();
        }
        else
        {
            logged.Exception.ShouldBeNull(); // the bridge logged it at Debug
            x.EventIds(LogLevel.Debug).ShouldContain(2000);
        }

        await x.DisposeAsync();
        x.Identifiers[^1].ShouldBe("devtoolsDisconnect");

        // Real sends answer later, so several may fail in flight: still one Warning. No context is captured in tests and
        // the answer doesn't force asynchronous continuations, so both run inside SetException.
        var answer = new TaskCompletionSource<object?>();
        await using var pending = new DevToolsHarness(static d => d.UseDevTools());
        pending.OnSend = args => ((string)args[1]!).Contains("StoreInitialized", StringComparison.Ordinal) ? true : answer.Task;
        await pending.InitializeAsync();
        pending.Store.Dispatch(new Increment());
        pending.Store.Dispatch(new Increment());
        await pending.Store.WhenIdleAsync(Ct);
        pending.Middleware.Sends.ShouldBe(3);
        answer.SetException(new JSException("rejected"));
        pending.EventIds(LogLevel.Warning).ShouldBe([2063]);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchIncrement(IStore store) => store.Dispatch(new Increment());
}
