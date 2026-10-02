using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.EffectFixtures;
using Ducky.Tests.InitFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §6.4 "What runs after a veto or a failure": a BeforeReduce or reducer failure commits nothing and notifies no one,
// but AfterReduce still sees the action (State == PreviousState, no changed key) and its effects still start; INV-08, INV-13.
public sealed class ProcessFailureTests
{
    [Fact]
    public async Task BeforeReduceThrows_NoCommitNoNotify_EffectsStart()
    {
        var journal = new List<string>();
        var throwing = new Recorder("throwing", journal) { OnBefore = ThrowOnBump };
        var last = new Recorder("last", journal);
        List<ActionContext> triggers = [];
        var effect = new Handler<Bump>((_, context, _) =>
        {
            triggers.Add(context.Trigger);
            return Task.CompletedTask;
        });
        var slice = new CountSlice();
        var store = new DuckyStore([slice], NullLogger.Instance, middleware: () => [throwing, last], effects: () => [(effect, false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<int> notified = [];
        using var selection = store.Select(s => s.Get<Count>().Value, notified.Add);
        var before = store.State;
        journal.Clear();
        var notifies = 0; // the unchanged snapshot hides a step-10 run from onChange; the seam does not
        store.Dispatcher.BeforeNotifyHook = () => notifies++;

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Failed);

        store.State.ShouldBeSameAs(before);
        slice.Reduced.ShouldBeEmpty();
        notified.ShouldBeEmpty();
        notifies.ShouldBe(0);
        journal.Where(e => e.EndsWith(" Bump", StringComparison.Ordinal)).ShouldBe(["throwing.MayDispatch Bump", "last.MayDispatch Bump", "throwing.BeforeReduce Bump", "throwing.AfterReduce Bump", "last.AfterReduce Bump"]);
        var after = last.Contexts.Last(c => c.Action is Bump);
        after.State.ShouldBeSameAs(before);
        after.PreviousState.ShouldBeSameAs(before);
        after.ChangedKeys.ShouldBeEmpty();
        triggers.ShouldHaveSingleItem().ShouldBeSameAs(after);
    }

    // Ducky.Tests half: a Merge Effect<StoreInitialized> returning at once stands in for ReactiveHost (§14); the
    // Ducky.Reactive.Tests twin asserts the pipelines subscribe.
    [Fact]
    public async Task ReducerThrowsOnStoreInitialized_LoadEffectAndReactiveHostStillStart()
    {
        var trail = new TrailSlice { OnInit = () => throw new InvalidOperationException("init") };
        var failures = new FailureSlice();
        var observer = new Recorder("observer", []);
        var load = new Handler<StoreInitialized>((_, context, _) => context.DispatchAsync(new Mark("loaded")));
        List<ActionContext> hostTriggers = [];
        var host = new Handler<StoreInitialized>((_, context, _) =>
        {
            hostTriggers.Add(context.Trigger);
            return Task.CompletedTask;
        });
        var store = new DuckyStore(
            [trail, failures],
            NullLogger.Instance,
            middleware: () => [observer],
            effects: () => [(load, false), (host, false)]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        failures.Failures.ShouldHaveSingleItem().ActionType.ShouldBe(typeof(StoreInitialized).ToString());
        store.State.Get<Trail>().Steps.ShouldBe(["loaded"]); // StoreInitialized committed nothing; its load effect ran
        var initialized = observer.Contexts.Last(c => c.Action is StoreInitialized); // its AfterReduce context
        initialized.State.ShouldBeSameAs(initialized.PreviousState);
        initialized.ChangedKeys.ShouldBeEmpty();
        hostTriggers.ShouldHaveSingleItem().ShouldBeSameAs(initialized);
    }

    private static void ThrowOnBump(ActionContext context)
    {
        if (context.Action is Bump)
        {
            throw new InvalidOperationException("before");
        }
    }
}
