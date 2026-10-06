using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
#pragma warning disable BL0006 // justification: see StubRenderer
using Microsoft.AspNetCore.Components.RenderTree;
#pragma warning restore BL0006
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Fakes;

// The I/O-boundary fakes behave as the real boundary does (SPEC §17.2); non-normative.
public sealed class FakesTests
{
    [Fact]
    public async Task FakeComponentStateStore_DrivesRealPersistenceManager()
    {
        var store = new FakeComponentStateStore();
        store.State["ducky:seed"] = JsonSerializer.SerializeToUtf8Bytes("seeded");
        var manager = new ComponentStatePersistenceManager(
            NullLogger<ComponentStatePersistenceManager>.Instance, new ServiceCollection().BuildServiceProvider());

        await manager.RestoreStateAsync(store);

        manager.State.TryTakeFromJson<string>("ducky:seed", out var seed).ShouldBeTrue();
        seed.ShouldBe("seeded");

        // A persist replaces the whole state, and keeps only what callbacks of Interactive Auto registrations persisted:
        // the restored seed, not persisted again, is gone.
        manager.State.RegisterOnPersisting(() => Persist("ducky:auto"), RenderMode.InteractiveAuto);
        manager.State.RegisterOnPersisting(() => Persist("ducky:server"), RenderMode.InteractiveServer);
        manager.State.RegisterOnPersisting(() => Persist("ducky:wasm"), RenderMode.InteractiveWebAssembly);
        await using var renderer = new StubRenderer();

        await manager.PersistStateAsync(store, renderer);

        store.State.Keys.ShouldBe(["ducky:auto"]);
        store.RenderModes.ShouldBe([RenderMode.InteractiveAuto, RenderMode.InteractiveServer, RenderMode.InteractiveWebAssembly], ignoreOrder: true);

        Task Persist(string key)
        {
            manager.State.PersistAsJson(key, key);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task FakeJsStreamReference_ReadsUpToMaxAllowedSize()
    {
        var reference = new FakeJsStreamReference(Encoding.UTF8.GetBytes("{}"));

        reference.Length.ShouldBe(2);
        reference.Opened.ShouldBeFalse();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(reference.OpenReadStreamAsync(maxAllowedSize: 1, TestContext.Current.CancellationToken).AsTask());
        await using (reference)
        {
            using var reader = new StreamReader(await reference.OpenReadStreamAsync(maxAllowedSize: 2, TestContext.Current.CancellationToken));
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("{}");
        }

        reference.Disposed.ShouldBeTrue();

        var dropped = new FakeJsStreamReference([0]) { OnDispose = new JSDisconnectedException("gone") };
        (await Should.ThrowAsync<JSDisconnectedException>(dropped.DisposeAsync().AsTask())).ShouldBeSameAs(dropped.OnDispose);
        dropped.Disposed.ShouldBeTrue();
    }

    [Fact]
    public async Task FakeJsRuntime_HonoursCancellation_NullIsDefault()
    {
        // As the real runtime: a call with a cancelled token fails as TaskCanceledException and never crosses; a pending call
        // fails so when its token is cancelled; a null result is the default of a value type.
        var js = new FakeJsRuntime();
        TaskCompletionSource<object?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Respond = (identifier, _) => identifier == "pending" ? pending.Task : null;
        using var cts = new CancellationTokenSource();

        (await js.InvokeAsync<bool>("flag", [])).ShouldBeFalse();
        var call = js.InvokeAsync<bool>("pending", cts.Token, []).AsTask();
        await cts.CancelAsync();
        await Should.ThrowAsync<TaskCanceledException>(call);
        await Should.ThrowAsync<TaskCanceledException>(js.InvokeAsync<bool>("flag", cts.Token, []).AsTask());

        // The interop timeout reaches only token-free calls, pending or new.
        var timed = js.InvokeAsync<bool>("pending", []).AsTask();
        await js.DefaultTimeout.CancelAsync();
        await Should.ThrowAsync<TaskCanceledException>(timed);
        await Should.ThrowAsync<TaskCanceledException>(js.InvokeAsync<bool>("flag", []).AsTask());
        (await js.InvokeAsync<bool>("flag", CancellationToken.None, [])).ShouldBeFalse();
        js.Calls.Select(c => c.Identifier).ShouldBe(["flag", "pending", "pending", "flag"]);
    }

    // Only to drive ComponentStatePersistenceManager.PersistStateAsync, which takes a renderer for its dispatcher.
#pragma warning disable BL0006 // justification: a test-only stub renderer; no render tree is produced or read
    private sealed class StubRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Throw(exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
#pragma warning restore BL0006
}
