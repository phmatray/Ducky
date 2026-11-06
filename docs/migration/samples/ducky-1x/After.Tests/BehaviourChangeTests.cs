using Ducky;
using Ducky.Blazor;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky1x.After.Tests;

// The behaviour changes of docs/migration/ducky-1x.md, one test each (SPEC §22.1), on the After app's CounterSlice and
// fixtures of their own.
public sealed class BehaviourChangeTests
{
    private static class Fixtures
    {
        public sealed record Tally(int Count);

        public sealed record Touch;

        public sealed record Keep;

        public sealed class TallySlice : Slice<Tally>
        {
            public TallySlice()
            {
                On<Touch>(state => state with { }); // DUCKY011 (Info) flags this in an app
                On<Keep>(state => state);
            }

            protected override Tally Initial => new(0);
        }

        public record Notify(string Text);

        public sealed record UrgentNotify(string Text) : Notify(Text);

        public sealed record Notified(string Text);

        public sealed record Inbox(string? Last);

        public sealed class InboxSlice : Slice<Inbox>
        {
            public InboxSlice() => On<Notified>((_, action) => new Inbox(action.Text));

            protected override Inbox Initial => new(Last: null);
        }

        public sealed class NotifyEffect : Effect<Notify>
        {
            public override Task Handle(Notify action, EffectContext context, CancellationToken cancellationToken)
            {
                context.Dispatch(new Notified(action.Text));
                return Task.CompletedTask;
            }
        }

        public sealed record Fragile(int Count);

        public sealed class FragileSlice : Slice<Fragile>
        {
            public FragileSlice() => On<Increment>((_, _) => throw new InvalidOperationException("boom"));

            protected override Fragile Initial => new(0);
        }

        public sealed record Ping;

        public sealed record Pings(int Count);

        public sealed class PingSlice : Slice<Pings>
        {
            public PingSlice() => On<Ping>(state => new Pings(state.Count + 1));

            protected override Pings Initial => new(0);
        }

        public sealed class PingAgain : Effect<Ping>
        {
            public override Task Handle(Ping action, EffectContext context, CancellationToken cancellationToken)
            {
                context.Dispatch(new Ping()); // synchronously, in the same causal chain
                return Task.CompletedTask;
            }
        }

        public sealed class Gate
        {
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(); // inline continuations: the releasing thread runs them
        }

        public sealed record Load;

        public sealed class LoadEffect(Gate gate) : Effect<Load>
        {
            public override async Task Handle(Load action, EffectContext context, CancellationToken cancellationToken)
            {
                gate.Started.SetResult();
                await gate.Release.Task.ConfigureAwait(false); // the I/O completes on another thread
                context.Dispatch(new Increment(1));
            }
        }

        public sealed class SlowInit(Gate gate) : Middleware
        {
            public override async ValueTask InitializeAsync(CancellationToken cancellationToken) =>
                await gate.Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    #region change-detection
    [Fact]
    public async Task ChangeDetection_EqualButNewRecord_IsAChange()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<Fixtures.TallySlice>());
        var before = store.State;

        await store.DispatchAsync(new Fixtures.Touch()); // returns `state with { }`: equal, but a new reference
        store.State.Get<Fixtures.Tally>().ShouldBe(before.Get<Fixtures.Tally>());
        store.State.Version.ShouldBe(before.Version + 1); // committed and notified: 1.x compared with Equals

        await store.DispatchAsync(new Fixtures.Keep()); // returns the same instance
        store.State.Version.ShouldBe(before.Version + 1); // no change
    }
    #endregion

    #region exact-type
    [Fact]
    public async Task HandlerMatching_Subtype_NotSeenByBaseTypeEffect()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<Fixtures.InboxSlice>().AddEffect<Fixtures.NotifyEffect>());

        store.Dispatch(new Fixtures.UrgentNotify("fire")); // an Effect<Notify> no longer sees UrgentNotify : Notify
        await store.Settled();
        store.State.Get<Fixtures.Inbox>().Last.ShouldBeNull();

        store.Dispatch(new Fixtures.Notify("mail"));
        await store.Settled();
        store.State.Get<Fixtures.Inbox>().Last.ShouldBe("mail");
    }
    #endregion

    #region sync-dispatch
    [Fact]
    public async Task SyncDispatch_FromAnotherThreadWhileDraining_ReturnsBeforeReduced()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>());
        var draining = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var selection = store.Select(s => s.Get<CounterState>().Value, value =>
        {
            if (value == 1)
            {
                draining.SetResult();
                release.Wait(); // holds the drain: Increment(10) can't be reduced until released
            }
        });
        var drainer = new Thread(() => store.Dispatch(new Increment(1)));
        drainer.Start();
        await draining.Task;

        var returned = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var caller = new Thread(() =>
        {
            store.Dispatch(new Increment(10)); // another thread drains: the action is only queued
            returned.SetResult(store.State.Get<CounterState>().Value);
        });
        try
        {
            caller.Start();
            var seenOnReturn = await returned.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            seenOnReturn.ShouldBe(1); // a 1.x dev-build Dispatch blocked here until the drain ended
        }
        finally
        {
            release.Set();
            drainer.Join();
            caller.Join();
        }

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.State.Get<CounterState>().Value.ShouldBe(11); // reduced by the drainer; await DispatchAsync to read your writes
    }
    #endregion

    #region reducer-failed
    [Fact]
    public async Task ReducerThrows_ReducerFailedDispatched_NothingCommitted()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>().AddSlice<Fixtures.FragileSlice>());

        var result = await store.DispatchAsync(new Increment(5)); // never rethrown

        result.ShouldBe(DispatchResult.Failed);
        store.State.Get<CounterState>().Value.ShouldBe(0); // CounterSlice's reduction is not committed either
        var failure = store.Failures.ShouldHaveSingleItem().ShouldBeOfType<ReducerFailed>();
        failure.Exception.Message.ShouldBe("boom");
    }
    #endregion

    #region dispatch-depth
    [Fact]
    public async Task MaxDispatchDepth_LimitsSynchronousCausalChain()
    {
        await using var store = TestStore.Create(ducky =>
        {
            ducky.MaxDispatchDepth = 3; // 1.x MaxReentrantDepth bounded the queue; this bounds the synchronous chain
            ducky.AddSlice<Fixtures.PingSlice>().AddEffect<Fixtures.PingAgain>();
        });

        store.Dispatch(new Fixtures.Ping());
        await store.Settled();

        store.State.Get<Fixtures.Pings>().Count.ShouldBe(4); // depths 0 to 3
        store.Failures.ShouldHaveSingleItem().ShouldBeOfType<ReducerFailed>().Exception.ShouldBeOfType<DispatchLoopException>();
    }
    #endregion

    #region subscriber-thread
    [Fact]
    public async Task Subscriber_RunsOnTheDrainingThread()
    {
        var gate = new Fixtures.Gate();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<Fixtures.LoadEffect>(),
            services => services.AddSingleton(gate));
        int? notifiedOn = null;
        using var selection = store.Select(s => s.Get<CounterState>().Value, _ => notifiedOn = Environment.CurrentManagedThreadId);

        store.Dispatch(new Fixtures.Load());
        await gate.Started.Task;
        var io = new Thread(gate.Release.SetResult); // stands for the thread pool thread that completes real I/O
        io.Start();
        io.Join();

        notifiedOn.ShouldBe(io.ManagedThreadId); // not the renderer: marshal UI work with InvokeAsync
    }
    #endregion

    #region init-buffer
    [Fact]
    public async Task DispatchBeforeInit_IsBuffered_ReducedOnceReady()
    {
        var gate = new Fixtures.Gate();
        await using var provider = new ServiceCollection()
            .AddSingleton(gate)
            .AddDucky(ducky => ducky.AddSlice<CounterSlice>().Use<Fixtures.SlowInit>())
            .BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        store.Dispatch(new Increment(3)); // starts init and waits in the init buffer
        store.State.Get<CounterState>().Value.ShouldBe(0);

        gate.Release.SetResult();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.State.Get<CounterState>().Value.ShouldBe(3);
    }
    #endregion

    #region use-json
    [Fact]
    public async Task Persist_WithoutUseJson_FailsAtStartupWithDucky306()
    {
        await using var without = new ServiceCollection()
            .AddDucky(ducky => ducky.AddSlice<CounterSlice>().Persist<CounterSlice>())
            .BuildServiceProvider();
        await using var scope = without.CreateAsyncScope();

        Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>())
            .Errors.ShouldContain(error => error.Code == "DUCKY306");

        await using var with = new ServiceCollection()
            .AddDucky(ducky => ducky.UseJson(AppJson.Default).AddSlice<CounterSlice>().Persist<CounterSlice>())
            .BuildServiceProvider();
        await using var withScope = with.CreateAsyncScope();
        withScope.ServiceProvider.GetRequiredService<IStore>().State.Get<CounterState>().Value.ShouldBe(0);
    }
    #endregion

    #region devtools-options
    [Fact]
    public void DevToolsOptions_Trimmed_TraceOffByDefault()
    {
        typeof(DevToolsOptions).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal)
            .ShouldBe(["ActionSanitizer", "Filter", "MaxAge", "Name", "StateSanitizer", "Trace", "TraceLimit"]);
        new DevToolsOptions().Trace.ShouldBeFalse();
    }
    #endregion
}
