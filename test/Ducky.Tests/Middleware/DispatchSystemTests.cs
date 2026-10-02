using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §5.2 (Origin.System), §5.6 (Middleware.DispatchSystem), §6.5; INV-06, INV-12.
public sealed class DispatchSystemTests
{
    [Fact]
    public async Task DispatchSystemLoopFromAfterReduce_IsStoppedByDepthGuard()
    {
        var journal = new List<string>();
        var looper = new Recorder("looper", journal) { Allow = _ => false };
        var failures = new FailureSlice();
        var store = new DuckyStore([new StepSlice(), failures], NullLogger.Instance, middleware: () => [looper]);
        looper.OnAfter = c =>
        {
            // Safety cap: without the guard this test fails instead of hanging.
            if (c.Action is StoreInitialized or Step && looper.Contexts.Count < 300)
            {
                looper.System(new Step("loop"));
            }
        };

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        // StoreInitialized is the root at depth 0; its AfterReduce starts the loop. Every Step is Origin.System, bypasses
        // the veto (MayDispatch is never consulted), and is a synchronous child on the root's chain: depths 1..64 are
        // reduced and the Step at depth 65 is dropped (MaxDispatchDepth is 64).
        journal.ShouldNotContain(e => e.Contains(".MayDispatch", StringComparison.Ordinal));
        var root = looper.Contexts.First(c => c.Action is StoreInitialized);
        var steps = looper.Contexts.Where(c => c.Action is Step).DistinctBy(c => c.Id).ToList();
        steps.Select(c => c.Depth).ShouldBe(Enumerable.Range(1, 64));
        steps.ShouldAllBe(c => c.Origin == Origin.System && c.CorrelationId == root.CorrelationId);

        // The drop's ReducerFailed(DispatchLoopException) is reduced, at depth 0 on the same chain.
        var failed = failures.Failures.ShouldHaveSingleItem();
        failed.ActionType.ShouldBe(typeof(Step).ToString());
        failed.Exception.ShouldBeOfType<DispatchLoopException>();
        var failure = looper.Contexts.First(c => c.Action is ReducerFailed);
        failure.Depth.ShouldBe(0);
        failure.CorrelationId.ShouldBe(root.CorrelationId);
    }

    // Non-normative (INV-12): isFailure marks a failure action, so a failure raised while handling it, or while handling
    // a synchronous descendant of it, is logged and never dispatched, whether it was sent from outside the drain or from a
    // hook (a synchronous child). Without isFailure the same throw is dispatched once.
    [Fact]
    public async Task DispatchSystem_IsFailure_FailuresUnderItLogOnly()
    {
        var logger = new FakeLogger();
        var system = new Recorder("system", []);
        var failures = new FailureSlice();
        var store = new DuckyStore([new BoomSlice(), failures], logger, middleware: () => [system]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        system.OnAfter = c =>
        {
            switch (c.Action)
            {
                case Ping when c.Depth == 0:
                    store.Dispatch(new Boom());
                    break;
                case Probe:
                    system.System(new Boom(), isFailure: true);
                    break;
            }
        };

        system.System(new Boom(), isFailure: true);
        system.System(new Ping(), isFailure: true);
        system.System(new Probe());

        failures.Failures.ShouldBeEmpty();
        var records = logger.Collector.GetSnapshot();
        records.Select(r => r.Id.Id).ShouldBe([1000, 1001, 1000, 1001, 1000, 1001]);
        records.ShouldAllBe(r => r.Level == LogLevel.Error);
        system.Contexts.Where(c => c.Action is Boom).Select(c => (c.Origin, c.Depth)).Distinct()
            .ShouldBe([(Origin.System, 0), (Origin.Local, 1), (Origin.System, 1)]);

        system.System(new Boom());

        failures.Failures.ShouldHaveSingleItem().ActionType.ShouldBe(typeof(Boom).ToString());
    }

    // Non-normative (§5.2): Origin.System bypasses the init buffer, so a DispatchSystem from a restore's AfterReduce is
    // reduced before the store is Ready, and it neither starts init nor needs it. A null action is rejected.
    [Fact]
    public void DispatchSystem_BeforeReady_BypassesInitBuffer()
    {
        var system = new Recorder("system", []);
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [system]);
        system.OnAfter = c =>
        {
            if (c.Origin == Origin.Hydration)
            {
                system.System(new Bump());
            }
        };

        store.Restore(new Dictionary<string, object> { ["count"] = new Count(1) }, Origin.Hydration);

        system.Contexts.Select(c => c.Action).ShouldNotContain(a => a is StoreInitialized);
        var bump = system.Contexts.Last();
        bump.Action.ShouldBeOfType<Bump>();
        bump.Origin.ShouldBe(Origin.System);
        bump.Depth.ShouldBe(1);
        bump.State.Get<Count>().Value.ShouldBe(2);
        Should.Throw<ArgumentNullException>(() => system.System(null!));
    }
}
