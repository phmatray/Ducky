using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.4 steps 1 and 6 (FailureRouter), §6.5; INV-06, INV-12.
public sealed class FailureRouterTests
{
    [Fact]
    public async Task ReducerFailed_ExactlyOnce()
    {
        var logger = new FakeLogger();
        var failures = new FailureSlice();
        var store = new DuckyStore([new CountSlice(), new BoomSlice(), new Boom2Slice(), failures], logger);
        var scopes = new List<Cause>();
        failures.OnFailed = _ => scopes.Add(store.Dispatcher.Causal.ShouldNotBeNull());
        store.Dispatch(new Bump());

        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);

        // One ReducerFailed for the action, enqueued as a core failure action at depth 0 on the failed action's chain.
        var failed = failures.Failures.ShouldHaveSingleItem();
        failed.ActionType.ShouldBe(typeof(Boom).ToString());
        failed.SliceKey.ShouldBe("boom");
        failed.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
        scopes.ShouldBe([new Cause(3, 0, 2, true)]);
        store.State.Get<Seen>().Count.ShouldBe(1);
        store.State.Get<Count>().Value.ShouldBe(1);
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1000);
        store.Dispatcher.Causal.ShouldBeNull();
    }

    [Fact]
    public async Task FailureWhileReducingFailure_LogsOnly()
    {
        var logger = new FakeLogger();
        var failures = new FailureSlice();
        var store = new DuckyStore([failures, new BoomSlice()], logger);

        // The failure action's reducer dispatches a synchronous child that throws, then throws itself.
        failures.OnFailed = _ =>
        {
            // Safety cap: without INV-12 each throw enqueues another ReducerFailed; this test fails instead of hanging.
            if (failures.Failures.Count > 1)
            {
                return;
            }

            store.Dispatch(new Boom());
            throw new InvalidOperationException("failure reducer");
        };

        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);

        // Only the first failure is dispatched; the reducer's throw and the child's are logged, never dispatched.
        failures.Failures.ShouldHaveSingleItem().ActionType.ShouldBe(typeof(Boom).ToString());
        store.State.Get<Seen>().Count.ShouldBe(0);
        var records = logger.Collector.GetSnapshot();
        records.Select(r => r.Id.Id).ShouldBe([1000, 1000, 1001, 1000, 1001]);
        records[2].Level.ShouldBe(LogLevel.Error);
        records[2].Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("failure reducer");
        records[2].Message.ShouldBe(
            $"{typeof(ReducerFailed)} for {typeof(ReducerFailed)} raised while handling a failure action, logged and not dispatched");
        records[4].Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
        records[4].Message.ShouldBe(
            $"{typeof(ReducerFailed)} for {typeof(Boom)} raised while handling a failure action, logged and not dispatched");

        // The failure chain ends with the failure action: the next unrelated action is routed again.
        failures.OnFailed = null;
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);
        failures.Failures.Count.ShouldBe(2);
        store.State.Get<Seen>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoopGuardFailure_IsReducedNotDropped()
    {
        var logger = new FakeLogger();
        var steps = new StepSlice();
        var failures = new FailureSlice();
        var store = new DuckyStore([steps, failures], logger);
        var scopes = new List<Cause>();
        var count = 0;
        steps.OnStep = _ =>
        {
            // Safety cap: without the guard this test fails instead of hanging.
            if (++count < 100)
            {
                store.Dispatch(new Step("loop"));
            }
        };
        failures.OnFailed = _ => scopes.Add(store.Dispatcher.Causal.ShouldNotBeNull());

        // Two unrelated actions first, so the loop's correlation id (3) differs from every Id the router could pick up.
        store.Dispatch(new Bump());
        store.Dispatch(new Bump());
        (await store.DispatchAsync(new Step("loop"))).ShouldBe(DispatchResult.Reduced);

        count.ShouldBe(65);
        var failed = failures.Failures.ShouldHaveSingleItem();
        failed.ActionType.ShouldBe(typeof(Step).ToString());
        failed.SliceKey.ShouldBeNull();
        var loop = failed.Exception.ShouldBeOfType<DispatchLoopException>();
        loop.ActionType.ShouldBe(typeof(Step).ToString());
        loop.Depth.ShouldBe(65);

        // Ids 3..68 are the loop (the last one dropped); the failure is Id 69, depth 0, on the loop's chain.
        scopes.ShouldBe([new Cause(69, 0, 3, true)]);
        store.State.Get<Seen>().Count.ShouldBe(1);
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task LoopGuardFailure_ReactorRedispatches_Terminates()
    {
        var logger = new FakeLogger();
        var steps = new StepSlice();
        var failures = new FailureSlice();
        var store = new DuckyStore([steps, failures], logger);
        var depths = new List<int>();
        steps.OnStep = _ =>
        {
            depths.Add(store.Dispatcher.Causal!.Depth);

            // Safety cap: without INV-12 the reactor loops forever, and this test fails instead of hanging.
            if (depths.Count < 1000)
            {
                store.Dispatch(new Step("loop"));
            }
        };

        // The reactor restarts the loop from the failure action, so the second loop descends from a failure action.
        failures.OnFailed = _ => store.Dispatch(new Step("loop"));

        (await store.DispatchAsync(new Step("loop"))).ShouldBe(DispatchResult.Reduced);

        // The first drop is reduced as a failure; the second loop runs at depths 1..64 and its drop is only logged.
        depths.ShouldBe([.. Enumerable.Range(0, 65), .. Enumerable.Range(1, 64)]);
        failures.Failures.ShouldHaveSingleItem().Exception.ShouldBeOfType<DispatchLoopException>().Depth.ShouldBe(65);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1001);
        record.Exception.ShouldBeOfType<DispatchLoopException>().Depth.ShouldBe(65);
        record.Message.ShouldBe(
            $"{typeof(ReducerFailed)} for {typeof(Step)} raised while handling a failure action, logged and not dispatched");

        // The store is still live.
        steps.OnStep = null;
        (await store.DispatchAsync(new Step("next"))).ShouldBe(DispatchResult.Reduced);
        store.Dispatcher.Causal.ShouldBeNull();
    }
}
