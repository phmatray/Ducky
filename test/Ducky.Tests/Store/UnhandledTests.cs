using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Load = Ducky.Tests.EffectFixtures.Load;

namespace Ducky.Tests;

// SPEC §6.4 step 12 (CORE-08), §5.1 ThrowOnUnhandledAction; ADR-0018.
public sealed class UnhandledTests
{
    private const string UnhandledMessage = "matched no reducer or async effect (reactive effects and middleware not inspected)";

    // An action no slice handles and no async effect matched is logged at Debug (EventId 1080) and still completes Reduced.
    // A reducer or a matched effect makes it handled; library traffic (System, and HydrateSlices restores from Hydration,
    // CrossTab and DevTools) is never checked; the log is guarded by IsEnabled.
    [Fact]
    public async Task UnhandledAction_DefaultLogsDebug()
    {
        var logger = new FakeLogger();
        var system = new Recorder("system", []);
        var store = new DuckyStore(
            [new CountSlice()],
            logger,
            middleware: () => [system],
            effects: () => [(new Handler<Matched>((_, _, _) => Task.CompletedTask), false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        (await store.DispatchAsync(new Unknown())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Matched())).ShouldBe(DispatchResult.Reduced);
        system.System(new Unknown());
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(5) }, Origin.Hydration);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(6) }, Origin.CrossTab);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(7) }, Origin.DevTools);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<Count>().Value.ShouldBe(7);

        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level).ShouldBe((1080, LogLevel.Debug));
        record.Message.ShouldBe($"Action {typeof(Unknown)} {UnhandledMessage}");
        record.Exception.ShouldBeNull();

        logger.ControlLevel(LogLevel.Debug, enabled: false);
        (await store.DispatchAsync(new Unknown())).ShouldBe(DispatchResult.Reduced);
        logger.Collector.Count.ShouldBe(1);
    }

    // With ThrowOnUnhandledAction (set on the builder), an unhandled Local or Effect action still completes Reduced and
    // routes one ReducerFailed(type, null, UnhandledActionException) through the FailureRouter, with no Debug log.
    // StoreInitialized (System) is never checked.
    [Fact]
    public async Task UnhandledAction_StrictMode_ReducerFailed()
    {
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddFakeLogging().SetMinimumLevel(LogLevel.Debug))
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.ThrowOnUnhandledAction = true;
                d.AddSlice<CountSlice>();
                d.AddSlice<FailureSlice>();
                d.AddEffect(new Handler<Load>((_, context, _) => context.DispatchAsync(new Unknown())));
            });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var failures = scope.ServiceProvider.GetRequiredService<FailureSlice>();
        var ct = TestContext.Current.CancellationToken;
        await store.InitializeAsync(ct);

        (await store.DispatchAsync(new Unknown())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced); // its effect dispatches Unknown
        await store.WhenIdleAsync(ct);

        failures.Failures.Count.ShouldBe(2);
        foreach (var failed in failures.Failures)
        {
            failed.ActionType.ShouldBe(typeof(Unknown).ToString());
            failed.SliceKey.ShouldBeNull();
            failed.Exception.ShouldBeOfType<UnhandledActionException>().ActionType.ShouldBe(typeof(Unknown).ToString());
        }

        store.State.Get<Count>().Value.ShouldBe(1);
        provider.GetFakeLogCollector().GetSnapshot().ShouldBeEmpty();
    }

    // Non-normative (§6.4, INV-08): a BeforeReduce or reducer failure skips step 12, so a failed unhandled action yields
    // only its own ReducerFailed.
    [Fact]
    public async Task UnhandledAction_StrictMode_FailedActionNotChecked()
    {
        var logger = new FakeLogger();
        var failures = new FailureSlice();
        var hooks = new Recorder("hooks", []);
        var store = new DuckyStore([failures], logger, middleware: () => [hooks], throwOnUnhandledAction: true);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        hooks.OnBefore = c =>
        {
            if (c.Action is Unknown)
            {
                throw new InvalidOperationException("before");
            }
        };

        (await store.DispatchAsync(new Unknown())).ShouldBe(DispatchResult.Failed);

        failures.Failures.ShouldHaveSingleItem().Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("before");
    }

    private sealed record Unknown;

    private sealed record Matched;
}
