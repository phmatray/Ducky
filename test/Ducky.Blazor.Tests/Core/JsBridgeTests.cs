using System.Reflection;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Core;

// SPEC §10 (JS interop takes only primitives), §11.5 (lazy module import, disconnect catches), §11.9; INV-23.
public sealed class JsBridgeTests
{
    private static readonly Type[] _crossingTypes = [typeof(string), typeof(int), typeof(long), typeof(bool)];

    [Fact]
    public async Task JsBridge_OnlyStringAndPrimitiveArgsCrossBoundary()
    {
        var js = new FakeJsRuntime();
        js.Respond = (identifier, _) => identifier == "import" ? js : true;
        await using var bridge = new JsBridge(js, NullLogger.Instance);
        using var target = DotNetObjectReference.Create(new InteropTarget());

        (await bridge.TryInvokeAsync<bool>("storageSet", TestContext.Current.CancellationToken, "local", 7, 8L, true, JsArg.Ref(target), null))
            .ShouldBe((true, true));

        // Every argument the runtime received, the import's included, is a string, int, long, bool or DotNetObjectReference.
        js.Calls.Select(call => call.Identifier).ShouldBe(["import", "storageSet"]);
        js.Calls[0].Args.ShouldBe([JsBridge.ModulePath]);
        js.Calls[1].Args.ShouldBe(["local", 7, 8L, true, target, null]);
        js.Calls.SelectMany(call => call.Args).ShouldAllBe(arg => arg == null || CrossesBoundary(arg.GetType()));

        // And nothing else can be passed: JsBridge's members take only those types (as JsArg), and JsArg converts only from them.
        var parameters = typeof(JsBridge).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic || method.IsAssembly)
            .SelectMany(method => method.GetParameters(), (_, parameter) => parameter.ParameterType)
            .ToList();
        parameters.ShouldNotBeEmpty();
        parameters.ShouldAllBe(type => type == typeof(string) || type == typeof(CancellationToken) || type == typeof(JsArg[]));
        typeof(JsArg).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(method => method.GetParameters().ShouldHaveSingleItem().ParameterType)
            .ShouldAllBe(type => CrossesBoundary(type));
    }

    [Fact]
    public async Task JsBridge_Import_CachesOnlySuccessfulImport()
    {
        // A faulted or cancelled import is dropped, so the next call imports afresh; a pending or successful one is shared.
        var js = new FakeJsRuntime();
        var collector = new FakeLogCollector();
        await using var bridge = new JsBridge(js, new FakeLogger(collector));
        Exception[] failures = [new JSDisconnectedException("gone"), new TaskCanceledException("timeout")];
        var imports = 0;
        TaskCompletionSource<object?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Respond = (identifier, _) => identifier switch
        {
            "import" => ++imports <= failures.Length ? Task.FromException<object?>(failures[imports - 1]) : pending.Task,
            _ => "value",
        };

        (await bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken)).ShouldBe((false, null));
        (await bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken)).ShouldBe((false, null));
        var first = bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken);
        var second = bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken);
        pending.SetResult(js);
        (await first).ShouldBe((true, "value"));
        (await second).ShouldBe((true, "value"));
        (await bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken)).ShouldBe((true, "value"));

        imports.ShouldBe(3);
        js.Calls.Count(call => call.Identifier == "import").ShouldBe(3);
        collector.GetSnapshot().Select(record => (record.Id.Id, record.Level, record.Message, record.Exception)).ShouldBe(
        [
            (2000, LogLevel.Debug, "JS interop call 'storageGet' interrupted: circuit disconnected or interop timed out", failures[0]),
            (2000, LogLevel.Debug, "JS interop call 'storageGet' interrupted: circuit disconnected or interop timed out", failures[1]),
        ]);
    }

    [Fact]
    public async Task JsBridge_CancelledDuringPendingImport_NotDelivered_ImportShared()
    {
        // The import, the step a dead circuit hangs, honours the caller's token; the shared import keeps running for others.
        var js = new FakeJsRuntime();
        var collector = new FakeLogCollector();
        await using var bridge = new JsBridge(js, new FakeLogger(collector));
        TaskCompletionSource<object?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Respond = (identifier, _) => identifier == "import" ? pending.Task : "value";
        using var cts = new CancellationTokenSource();

        var call = bridge.TryInvokeAsync<string>("storageGet", cts.Token);
        await cts.CancelAsync();

        (await call).ShouldBe((false, null));
        var record = collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Message).ShouldBe((2000, "JS interop call 'storageGet' interrupted: circuit disconnected or interop timed out"));
        pending.SetResult(js);
        (await bridge.TryInvokeAsync<string>("storageGet", TestContext.Current.CancellationToken)).ShouldBe((true, "value"));
        js.Calls.Select(c => c.Identifier).ShouldBe(["import", "storageGet"]);
    }

    [Fact]
    public async Task JsBridge_CallInterrupted_NotDelivered_ModuleKept()
    {
        var js = new FakeJsRuntime();
        var collector = new FakeLogCollector();
        await using var bridge = new JsBridge(js, new FakeLogger(collector));
        js.Respond = (identifier, _) => identifier switch
        {
            "import" => js,
            "disconnected" => throw new JSDisconnectedException("gone"),
            "cancelled" => Task.FromException<object?>(new TaskCanceledException()),
            _ => throw new JSException("storage is full"),
        };

        (await bridge.TryInvokeAsync<bool>("disconnected", TestContext.Current.CancellationToken)).ShouldBe((false, false));
        (await bridge.TryInvokeAsync<bool>("cancelled", TestContext.Current.CancellationToken)).ShouldBe((false, false));

        // Only a disconnect or an interop timeout is caught: a JS error is the caller's to handle.
        (await Should.ThrowAsync<JSException>(bridge.TryInvokeAsync<bool>("storageSet", TestContext.Current.CancellationToken).AsTask())).Message.ShouldBe("storage is full");
        js.Calls.Count(call => call.Identifier == "import").ShouldBe(1);
        collector.GetSnapshot().Select(record => (record.Id.Id, record.Message, record.Exception?.GetType())).ShouldBe(
        [
            (2000, "JS interop call 'disconnected' interrupted: circuit disconnected or interop timed out", typeof(JSDisconnectedException)),
            (2000, "JS interop call 'cancelled' interrupted: circuit disconnected or interop timed out", typeof(TaskCanceledException)),
        ]);
    }

    [Fact]
    public async Task JsBridge_ModuleCall_KeepsInteropTimeout_CallerTokenBoundsItsWaitOnly()
    {
        // §8.1, §11.5: a module call keeps the runtime's interop timeout (a token passed to the runtime would switch it off),
        // so a call left pending by a dropped circuit still ends as not delivered; the caller's token bounds its own wait.
        var js = new FakeJsRuntime();
        var collector = new FakeLogCollector();
        await using var bridge = new JsBridge(js, new FakeLogger(collector));
        TaskCompletionSource<object?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Respond = (identifier, _) => identifier switch
        {
            "import" => js,
            "storageGet" => pending.Task,
            _ => true,
        };
        using var cts = new CancellationTokenSource();

        var cancelled = bridge.TryInvokeAsync<string>("storageGet", cts.Token);
        await cts.CancelAsync();
        (await cancelled).ShouldBe((false, null));

        await js.DefaultTimeout.CancelAsync();
        (await bridge.TryInvokeAsync<bool>("storageSet", CancellationToken.None)).ShouldBe((false, false));

        collector.GetSnapshot().Select(record => (record.Id.Id, record.Level, record.Message, record.Exception?.GetType())).ShouldBe(
        [
            (2000, LogLevel.Debug, "JS interop call 'storageGet' interrupted: circuit disconnected or interop timed out", typeof(TaskCanceledException)),
            (2000, LogLevel.Debug, "JS interop call 'storageSet' interrupted: circuit disconnected or interop timed out", typeof(TaskCanceledException)),
        ]);
    }

    [Fact]
    public async Task JsBridge_Import_SynchronousThrow_ReachesCaller_NotCached()
    {
        // The prerender shape (§11.4): the runtime throws synchronously, and the caller sees it; the next call imports again.
        var js = new FakeJsRuntime();
        var throwing = true;
        js.Respond = (identifier, _) => throwing ? throw new InvalidOperationException("prerendering") : js;
        await using var bridge = new JsBridge(js, NullLogger.Instance);

        Should.Throw<InvalidOperationException>(() => bridge.ImportAsync());
        throwing = false;
        (await bridge.ImportAsync()).ShouldBeSameAs(js);
        bridge.ImportAsync().ShouldBeSameAs(bridge.ImportAsync());
        js.Calls.Count.ShouldBe(2);
    }

    [Fact]
    public async Task JsBridge_DisposeAsync_DisposesImportedModuleOnce_CatchesDisconnect()
    {
        // Nothing imported: nothing to dispose.
        var unused = new FakeJsRuntime();
        await new JsBridge(unused, NullLogger.Instance).DisposeAsync();
        unused.Disposed.ShouldBe(0);

        // A failed import leaves nothing to dispose either.
        var failed = new FakeJsRuntime { Respond = (_, _) => Task.FromException<object?>(new JSDisconnectedException("gone")) };
        var failedBridge = new JsBridge(failed, NullLogger.Instance);
        (await failedBridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeFalse();
        await failedBridge.DisposeAsync();
        failed.Disposed.ShouldBe(0);

        // A disconnected circuit: disposing the module throws JSDisconnectedException, which never escapes.
        var collector = new FakeLogCollector();
        var js = new FakeJsRuntime { OnDispose = new JSDisconnectedException("gone") };
        var bridge = new JsBridge(js, new FakeLogger(collector));
        (await bridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeTrue();
        await bridge.DisposeAsync();
        await bridge.DisposeAsync();
        js.Disposed.ShouldBe(1);
        var record = collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Message).ShouldBe((2000, "JS interop call 'dispose' interrupted: circuit disconnected or interop timed out"));
        record.Exception.ShouldBeSameAs(js.OnDispose);

        // Once disposed, a late call (a flush after DisposeTimeout, §11.5) is not delivered, silently, and imports nothing.
        (await bridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).ShouldBe((false, null));
        Should.Throw<ObjectDisposedException>(() => bridge.ImportAsync());
        (await failedBridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeFalse();
        js.Calls.Select(call => call.Identifier).ShouldBe(["import", "ready"]);
        failed.Calls.Count(call => call.Identifier == "import").ShouldBe(1);
        collector.GetSnapshot().Count.ShouldBe(1);
    }

    private static bool CrossesBoundary(Type type) =>
        _crossingTypes.Contains(type) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DotNetObjectReference<>));
}
