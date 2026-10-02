using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §5.1 (AddEffect), §5.5 (Effect, EffectContext), §6.4 step 11, §6.6 (materialization, Merge), §6.11 phase 5b;
// INV-11, INV-31.
public sealed class EffectTests
{
    // Registering the effect type in DI changes nothing: each store creates its own instance from its own scope, owns it
    // and disposes it, and never touches the DI instance (§5.6, §6.10).
    [Fact]
    public async Task Effect_RegisteredInDi_StillStoreOwnedPerStore()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection()
            .AddSingleton(journal)
            .AddSingleton<LoadEffect>()
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Scoped;
                d.AddSlice<SeenSlice>();
                d.AddEffect<LoadEffect>();
            });
        await using var provider = services.BuildServiceProvider();
        var registered = provider.GetRequiredService<LoadEffect>();
        var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();

        (await first.ServiceProvider.GetRequiredService<IStore>().DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        (await second.ServiceProvider.GetRequiredService<IStore>().DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Reduced);

        journal.Handled.Select(h => h.Action).ShouldBe([new Load(1), new Load(2)]);
        var a = journal.Handled[0].Effect.ShouldBeOfType<LoadEffect>();
        var b = journal.Handled[1].Effect.ShouldBeOfType<LoadEffect>();
        a.ShouldNotBeSameAs(registered);
        b.ShouldNotBeSameAs(registered);
        b.ShouldNotBeSameAs(a);

        await first.DisposeAsync();
        a.Disposed.ShouldBeTrue();
        b.Disposed.ShouldBeFalse();
        registered.Disposed.ShouldBeFalse();
    }

    // Store disposal cancels a Merge run's token, which is the store-lifetime token itself; the run's resulting
    // OperationCanceledException is our cancellation, never EffectFailed (neither enqueued, which would log 1002 once
    // disposed, nor logged as a failure under a failure, 1001).
    [Fact]
    public async Task Effect_PolicyCancellation_NeverEffectFailed()
    {
        var logger = new FakeLogger();
        var release = new TaskCompletionSource(); // SetResult resumes the run inline, on the test thread
        CancellationToken runToken = default;
        var effect = new Handler<Load>(async (_, _, token) =>
        {
            runToken = token;
            await release.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        store.Dispatch(new Load(1));
        runToken.ShouldBe(store.Dispatcher.Lifetime);
        runToken.IsCancellationRequested.ShouldBeFalse();

        await store.DisposeAsync();
        runToken.IsCancellationRequested.ShouldBeTrue();
        release.SetResult();

        logger.Collector.GetSnapshot().ShouldBeEmpty();
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1)]);
        effect.Policy.ShouldBe(Concurrency.Merge);
        effect.LongRunning.ShouldBeFalse();
        effect.KeyOf(new Load(1)).ShouldBeNull();
    }

    // A constructor that throws at materialization (first store use, never at resolution) becomes one cached
    // DuckyConfigurationException (DUCKY353) wrapping the original, and every entry point rethrows that same instance
    // synchronously, without running the constructor again (§5.1, §6.6, §7 rule 4).
    [Fact]
    public async Task Materialization_CtorThrows_EveryCallRethrowsSameConfigurationException()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection().AddSingleton(journal).AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<SeenSlice>();
            d.AddEffect<ThrowingEffect>();
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        journal.Constructed.ShouldBeEmpty();

        var thrown = Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Load(1)));

        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY353");
        thrown.Message.ShouldContain("Ducky.Tests.EffectFixtures.ThrowingEffect");
        thrown.InnerException.ShouldBeOfType<FormatException>().Message.ShouldBe("ctor");
        var ct = TestContext.Current.CancellationToken;
        Action[] calls =
        [
            () => store.Dispatch(new Load(2)),
            () => _ = store.DispatchAsync(new Load(3)),
            () => store.Restore(new Dictionary<string, object>(), Origin.Hydration),
            () => _ = store.State,
            () => _ = store.InitializeAsync(ct),
            () => _ = store.WhenIdleAsync(ct),
            () => _ = store.Select(s => s),
            () => _ = store.Select(s => s, _ => { }),
        ];
        foreach (var call in calls)
        {
            Should.Throw<DuckyConfigurationException>(call).ShouldBeSameAs(thrown);
        }

        journal.Constructed.ShouldBe([typeof(ThrowingEffect)]);
    }

    // Non-normative: the store uses an AddEffect(instance) instance but never disposes it (§5.1, §6.11 5b).
    [Fact]
    public async Task Effect_AddEffectInstance_NeverDisposed()
    {
        var instance = new DisposableHandler();
        var services = new ServiceCollection().AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<SeenSlice>();
            d.AddEffect(instance);
        });
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IStore>().DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        await scope.DisposeAsync();

        instance.Handled.ShouldBe([new Load(1)]);
        instance.Disposed.ShouldBeFalse();
    }

    // One registration per effect type: the instance wins over the type registration whatever the call order, so the
    // type's constructor never runs and the handler runs once per action (§5.1). The registration is keyed by TEffect,
    // so a test double derived from it, passed as AddEffect<LoadEffect>(fake), takes the same runner (§15).
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task AddEffect_InstanceAndType_SameEffect_OneRunner(bool instanceFirst, bool derived)
    {
        var journal = new EffectJournal();
        var instance = derived ? new FakeLoadEffect(journal) : new LoadEffect(journal);
        journal.Constructed.Clear();
        var services = new ServiceCollection().AddSingleton(journal).AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<SeenSlice>();
            _ = instanceFirst ? d.AddEffect(instance).AddEffect<LoadEffect>() : d.AddEffect<LoadEffect>().AddEffect(instance);
        });
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<IStore>().DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        await scope.DisposeAsync();

        journal.Handled.ShouldHaveSingleItem().ShouldBe((instance, new Load(1)));
        journal.Constructed.ShouldBeEmpty();
        instance.Disposed.ShouldBeFalse();
    }

    // Non-normative: step 11 starts the run inline, so a synchronous Dispatch by the drainer returns after the handler's
    // prefix ran. The context reads the live state, carries its action's ActionContext and the store's TimeProvider, and
    // dispatches with Origin.Effect: a synchronous child before the first real yield, a new depth budget on the same
    // chain after it (§5.5, §6.5).
    [Fact]
    public async Task EffectContext_LiveStateTriggerTimeAndEffectOrigin()
    {
        var time = new FakeTimeProvider();
        var recorder = new Recorder("r", []);
        var resume = new TaskCompletionSource();
        var finished = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        EffectContext? seen = null;
        var effect = new Handler<Load>(async (_, context, _) =>
        {
            seen = context;
            context.Dispatch(new Loaded(1));
            await resume.Task.ConfigureAwait(false);
            finished.SetResult(await context.DispatchAsync(new Loaded(2)).ConfigureAwait(false));
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, timeProvider: time, middleware: () => [recorder], effects: () => [(effect, false)]);

        store.Dispatch(new Load(7));

        var context = seen.ShouldNotBeNull();
        context.Trigger.Action.ShouldBe(new Load(7));
        context.Trigger.Origin.ShouldBe(Origin.Local);
        context.Trigger.State.Get<Seen>().Actions.ShouldBe([new Load(7)]);
        context.Time.ShouldBeSameAs(time);
        context.State.Get<Seen>().Actions.ShouldBe([new Load(7), new Loaded(1)]);

        resume.SetResult();
        (await finished.Task).ShouldBe(DispatchResult.Reduced);

        context.State.ShouldBeSameAs(store.State);
        context.State.Get<Seen>().Actions.ShouldBe([new Load(7), new Loaded(1), new Loaded(2)]);
        var dispatched = recorder.Contexts.Where(c => c.Action is Loaded).DistinctBy(c => c.Id).ToList();
        dispatched.Select(c => (c.Origin, c.Depth, c.CorrelationId)).ShouldBe(
        [
            (Origin.Effect, 1, context.Trigger.CorrelationId),
            (Origin.Effect, 0, context.Trigger.CorrelationId),
        ]);
    }

    // Non-normative: an effect that throws (here synchronously, before any await) is observed: EffectFailed is enqueued as
    // a System action at depth 0 on its action's chain. An OperationCanceledException while the store's token is not
    // cancelled is not our cancellation, so it fails too (§6.6, §8.1; the failure rules of INV-12 are M2-02's).
    public static TheoryData<Exception> EffectExceptions => new()
    {
        new InvalidOperationException("effect"),
        new OperationCanceledException("foreign"),
    };

    [Theory]
    [MemberData(nameof(EffectExceptions))]
    public async Task Effect_Throws_EffectFailedOnTriggerChain(Exception thrown)
    {
        var recorder = new Recorder("r", []);
        var effect = new Handler<Load>((_, _, _) => throw thrown);
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, middleware: () => [recorder], effects: () => [(effect, false)]);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        var failure = new EffectFailed(typeof(Handler<Load>).ToString(), typeof(Load).ToString(), thrown);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), failure]);
        var load = recorder.Contexts.First(c => c.Action is Load);
        var failed = recorder.Contexts.First(c => c.Action is EffectFailed);
        (failed.Origin, failed.Depth, failed.CorrelationId).ShouldBe((Origin.System, 0, load.CorrelationId));
    }

    // An OperationCanceledException carrying the run's own token is cancellation even before that token is cancelled, so
    // it is never EffectFailed (§6.6: ex.CancellationToken == runToken || runToken.IsCancellationRequested).
    [Fact]
    public async Task Effect_OceForOwnTokenNotYetCancelled_IsCancellation()
    {
        var logger = new FakeLogger();
        var effect = new Handler<Load>((_, _, token) => throw new OperationCanceledException(token));
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeFalse();
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1)]);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    // Non-normative: step 10 (Notify) runs before step 11 starts the action's effects (§6.4).
    [Fact]
    public async Task Process_NotifiesSubscribersBeforeStartingEffects()
    {
        List<string> order = [];
        var effect = new Handler<Load>((_, _, _) =>
        {
            order.Add("effect");
            return Task.CompletedTask;
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);
        using var selection = store.Select(s => s.Get<Seen>(), _ => order.Add("notify"));

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        order.ShouldBe(["notify", "effect"]);
    }

    // Non-normative: effects match the exact runtime type of the action, never a base type (ADR-0008).
    [Fact]
    public async Task Effect_MatchesExactActionTypeOnly()
    {
        List<Base> handled = [];
        var effect = new Handler<Base>((action, _, _) =>
        {
            handled.Add(action);
            return Task.CompletedTask;
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        (await store.DispatchAsync(new Derived())).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Base())).ShouldBe(DispatchResult.Reduced);

        handled.ShouldBe([new Base()]);
    }

    // Non-normative: phase 5b disposes the store-owned effects after the middleware, in reverse registration order, each
    // in its own try/catch (Error 1015): async-disposable, disposable, or neither (§6.11).
    [Fact]
    public async Task Dispose_StoreOwnedEffects_DisposedAfterMiddlewareInReverseOrder()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection().AddSingleton(journal).AddLogging(logging => logging.AddFakeLogging()).AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<SeenSlice>();
            d.AddEffect<LoadEffect>().AddEffect<FaultingDisposeEffect>().AddEffect<PlainEffect>();
            d.Use<JournalMiddleware>();
        });
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        _ = scope.ServiceProvider.GetRequiredService<IStore>().State;

        await scope.DisposeAsync();

        journal.Constructed.ShouldBe([typeof(LoadEffect), typeof(PlainEffect)]);
        journal.Disposed.ShouldBe([nameof(JournalMiddleware), nameof(FaultingDisposeEffect), nameof(LoadEffect)]);
        var record = provider.GetFakeLogCollector().GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level).ShouldBe((1015, LogLevel.Error));
        record.Message.ShouldBe($"Disposing {typeof(FaultingDisposeEffect)} threw");
        record.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("dispose");
    }

    // Non-normative: once disposal began, no entry point materializes, so no user constructor runs and none rethrows
    // DUCKY353 (§6.6; the race with a concurrent first use is M4-05b's).
    [Fact]
    public async Task Materialization_AfterDispose_ConstructsNothingAndNeverThrows()
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection().AddSingleton(journal).AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<SeenSlice>();
            d.AddEffect<LoadEffect>().AddEffect<ThrowingEffect>();
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        await store.DisposeAsync();
        var ct = TestContext.Current.CancellationToken;

        store.Dispatch(new Load(1));
        (await store.DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Disposed);
        store.Restore(new Dictionary<string, object>(), Origin.Hydration);
        store.State.ShouldBeSameAs(store.InitialState);
        await store.InitializeAsync(ct);
        await store.WhenIdleAsync(ct);
        store.Select(s => s).Value.ShouldBeSameAs(store.InitialState);
        store.Select(s => s, _ => { }).Value.ShouldBeSameAs(store.InitialState);

        journal.Constructed.ShouldBeEmpty();
    }
}
