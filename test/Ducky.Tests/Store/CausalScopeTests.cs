using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §6.4 steps 1-2, §6.5; INV-06. Reducers observe the scope they run under through Dispatcher.Causal.
public sealed class CausalScopeTests
{
    [Fact]
    public async Task SelfDispatchLoop_IsStoppedByDepthGuard()
    {
        var slice = new StepSlice();
        var store = new DuckyStore([slice], NullLogger.Instance);
        var scopes = new List<Cause>();
        var children = new List<Task<DispatchResult>>();
        slice.OnStep = _ =>
        {
            scopes.Add(store.Dispatcher.Causal.ShouldNotBeNull());

            // Safety cap: without the guard this test fails instead of hanging.
            if (scopes.Count < 100)
            {
                children.Add(store.DispatchAsync(new Step("loop")));
            }
        };

        (await store.DispatchAsync(new Step("loop"))).ShouldBe(DispatchResult.Reduced);

        // Depths 0..64 are reduced (MaxDispatchDepth is 64), all on one chain; the child at depth 65 is dropped.
        scopes.Select(s => s.Depth).ShouldBe(Enumerable.Range(0, 65));
        scopes.Select(s => s.Seq).ShouldBe(Enumerable.Range(1, 65).Select(i => (long)i));
        scopes.ShouldAllBe(s => s.CorrelationId == 1 && !s.InFailure);
        var results = await Task.WhenAll(children);
        results.Length.ShouldBe(65);
        results[..64].ShouldAllBe(r => r == DispatchResult.Reduced);
        results[64].ShouldBe(DispatchResult.Dropped);

        // The store is still live, and the next unrelated dispatch starts a new chain at depth 0.
        slice.OnStep = _ => scopes.Add(store.Dispatcher.Causal.ShouldNotBeNull());
        (await store.DispatchAsync(new Step("next"))).ShouldBe(DispatchResult.Reduced);
        scopes[^1].ShouldBe(new Cause(67, 0, 67, false));
    }

    // Non-normative: Process restores the drainer's scope and resets _processingSeq, so nothing leaks to the caller.
    [Fact]
    public void CausalScope_NeverLeftSetInCallerContext()
    {
        var slice = new StepSlice();
        var store = new DuckyStore([slice], NullLogger.Instance);
        var seen = new Dictionary<string, Cause?>();
        ExecutionContext? parentFlow = null;
        ExecutionContext? childFlow = null;
        slice.OnStep = step =>
        {
            seen[step.Name] = store.Dispatcher.Causal;
            switch (step.Name)
            {
                case "parent":
                    parentFlow = ExecutionContext.Capture();
                    store.Dispatch(new Step("child"));
                    break;
                case "child":
                    childFlow = ExecutionContext.Capture();
                    break;
                case "other":
                    // A flow carrying another action's scope while "other" is processed: not a synchronous child.
                    ExecutionContext.Run(parentFlow!, _ => store.Dispatch(new Step("foreign")), null);
                    break;
            }
        };

        store.Dispatch(new Step("parent"));

        store.Dispatcher.Causal.ShouldBeNull();
        var parent = new Cause(1, 0, 1, false);
        seen["parent"].ShouldBe(parent);
        var child = new Cause(2, 1, 1, false);
        seen["child"].ShouldBe(child);

        // A continuation on the flow of the last action processed, once the store is idle: _processingSeq is back to 0,
        // so it is no synchronous child but a new depth-0 chain with the same correlation. Its drain restores the scope
        // the continuation ran under, not null.
        ExecutionContext.Run(childFlow!, _ =>
        {
            store.Dispatch(new Step("continuation"));
            store.Dispatcher.Causal.ShouldBe(child);
        }, null);
        seen["continuation"].ShouldBe(new Cause(3, 0, 1, false));
        store.Dispatcher.Causal.ShouldBeNull();

        store.Dispatch(new Step("other"));

        seen["other"].ShouldBe(new Cause(4, 0, 4, false));
        seen["foreign"].ShouldBe(new Cause(5, 0, 1, false));
        store.Dispatcher.Causal.ShouldBeNull();
    }

    // Non-normative: the §6.5 InFailure rules. Step 2 copies it into the scope, a synchronous child inherits it and an
    // asynchronous continuation never does, so a failure after a real yield is routed, not only logged (INV-12).
    [Fact]
    public void InFailure_InheritedBySynchronousChildOnly()
    {
        var slice = new StepSlice();
        var store = new DuckyStore([slice], NullLogger.Instance);
        var seen = new Dictionary<string, Cause?>();
        ExecutionContext? childFlow = null;
        slice.OnStep = step =>
        {
            seen[step.Name] = store.Dispatcher.Causal;
            if (step.Name == "parent")
            {
                store.Dispatch(new Step("child"));
            }
            else if (step.Name == "child")
            {
                childFlow = ExecutionContext.Capture();
            }
        };

        // A failure chain seeded through the internal API, the way FailureRouter will enqueue one (M1-07).
        store.Dispatcher.Enqueue(new Pending(new Step("parent"), Origin.Local, 0, 0, true, null));

        seen["parent"].ShouldBe(new Cause(1, 0, 1, true));
        seen["child"].ShouldBe(new Cause(2, 1, 1, true));
        ExecutionContext.Run(childFlow!, _ => store.Dispatch(new Step("continuation")), null);
        seen["continuation"].ShouldBe(new Cause(3, 0, 1, false));
    }
}
