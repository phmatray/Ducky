using System.Diagnostics;
using Ducky.Testing.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Testing.Tests;

// SPEC §15 (TST-01, ADR-0039): TestStore on Ducky's public API.
public sealed class TestStoreTests
{
    // Seed restores with Origin.Hydration, which strict mode never checks; an unhandled user action still fails.
    [Fact]
    public async Task TestStore_Seed_NoFailure()
    {
        await using var store = TestStore.Create(d => d.AddSlice<CounterSlice>());

        store.Seed(new Counter(5)).ShouldBeSameAs(store);
        await store.Settled();

        store.State.Get<Counter>().ShouldBe(new Counter(5));
        store.State.WasRestored<Counter>().ShouldBeTrue();
        store.Processed.ShouldContain(c => c.Origin == Origin.Hydration);
        store.Failures.ShouldBeEmpty();

        await store.DispatchAsync(new Unhandled());
        await store.Settled();

        store.Failures.ShouldHaveSingleItem().ShouldBeOfType<ReducerFailed>().Exception.ShouldBeOfType<UnhandledActionException>();
    }

    // Failures holds ReducerFailed, EffectFailed and the listed failure types, in processing order, and nothing else;
    // Processed includes the failed action. Configure runs after strict mode is set, so it can opt out.
    [Fact]
    public async Task TestStore_FailureTypes_CollectsCustomFailures()
    {
        await using var store = TestStore.Create(
            d =>
            {
                d.ThrowOnUnhandledAction = false;
                d.AddSlice<CounterSlice>().AddEffect<QuotaEffect>().AddEffect<FailEffect>();
            },
            failureTypes: typeof(QuotaExceeded));
        var before = store.Processed;

        await store.DispatchAsync(new Increment());
        await store.DispatchAsync(new Boom());
        await store.DispatchAsync(new Fail());
        await store.DispatchAsync(new Unhandled());
        await store.Settled();

        before.ShouldBeEmpty();
        store.Failures.Count.ShouldBe(3);
        store.Failures[0].ShouldBe(new QuotaExceeded("full"));
        store.Failures[1].ShouldBeOfType<ReducerFailed>().Exception.Message.ShouldBe("boom");
        store.Failures[2].ShouldBeOfType<EffectFailed>().Exception.Message.ShouldBe("effect failed");
        var boom = store.Processed.Single(c => c.Action is Boom);
        boom.State.ShouldBeSameAs(boom.PreviousState);
        store.Processed.ShouldContain(c => c.Action is Unhandled);
        store.Processed.Single(c => c.Action is QuotaExceeded).Origin.ShouldBe(Origin.Effect);
    }

    // Settled waits for the effect, which waits on the store's FakeTimeProvider (Time), and for the action it dispatches.
    [Fact]
    public async Task TestStore_Settled_WaitsForEffects()
    {
        await using var store = TestStore.Create(d => d.AddSlice<CounterSlice>().AddEffect<LoadEffect>());

        await store.DispatchAsync(new Load());
        var settled = store.Settled();

        settled.IsCompleted.ShouldBeFalse();
        store.Time.Advance(TimeSpan.FromHours(1));
        await settled;

        store.State.Get<Counter>().Value.ShouldBe(100);
        store.Failures.ShouldBeEmpty();
    }

    // A Settled timeout is a DuckyAssertionException naming its real-time bound, not a TimeoutException.
    [Fact]
    public async Task Assertions_ThrowDuckyAssertionException()
    {
        await using var store = TestStore.Create(d => d.AddSlice<CounterSlice>().AddEffect<LoadEffect>());
        await store.DispatchAsync(new Load());

        var thrown = await Should.ThrowAsync<DuckyAssertionException>(() => store.Settled(TimeSpan.FromMilliseconds(100)));

        thrown.Message.ShouldContain("00:00:00.1000000");
        thrown.InnerException.ShouldBeOfType<TimeoutException>();
        new DuckyAssertionException().InnerException.ShouldBeNull();
        store.Time.Advance(TimeSpan.FromHours(1));
        await store.Settled();
    }

    // The key comes from the declared state type: a derived state never matches its base's slice, and a state type
    // with no slice throws DUCKY350.
    [Fact]
    public async Task TestStore_Seed_ResolvesKeyByDeclaredStateType()
    {
        await using var store = TestStore.Create(d => d.AddSlice<BaseSlice>().AddSlice<DerivedSlice>());

        store.Seed(new DerivedState("d", 1));

        store.State.Get<DerivedState>().ShouldBe(new DerivedState("d", 1));
        store.State.WasRestored<DerivedState>().ShouldBeTrue();
        store.State.WasRestored<BaseState>().ShouldBeFalse();

        store.Seed(new BaseState("b"));

        store.State.Get<BaseState>().ShouldBe(new BaseState("b"));
        store.State.WasRestored<BaseState>().ShouldBeTrue();
        store.State.Get<DerivedState>().ShouldBe(new DerivedState("d", 1));

        store.Seed<BaseState>(new DerivedState("x", 2));

        store.State.Get<BaseState>().ShouldBe(new DerivedState("x", 2));
        store.State.Get<DerivedState>().ShouldBe(new DerivedState("d", 1));

        var thrown = Should.Throw<DuckyConfigurationException>(() => store.Seed(new NotRegistered()));
        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY350");
        store.Failures.ShouldBeEmpty();
    }

    // GetSlice returns the SliceStore the store attached, so calling its methods changes the store's state.
    [Fact]
    public async Task TestStore_GetSlice_ReturnsStoreOwnedSliceStore()
    {
        await using var store = TestStore.Create(d => d.AddSlice<CartStore>());

        var cart = store.GetSlice<CartStore>();
        cart.Add("milk");
        await store.Settled();

        store.Slices.ShouldHaveSingleItem().ShouldBeSameAs(cart);
        store.GetSlice<CartStore>().ShouldBeSameAs(cart);
        store.State.Get<Cart>().Items.ShouldBe(["milk"]);
        store.Failures.ShouldBeEmpty();
    }

    // Every IStore member forwards to the real store, cancellation tokens included (only a cancelled caller token
    // cancels InitializeAsync or WhenIdleAsync, INV-10); the services callback reaches the container, and a
    // TimeProvider it registers never replaces Time as the store's clock.
    [Fact]
    public async Task TestStore_ForwardsStoreMembers()
    {
        var marker = new object();
        object? resolved = null;
        var initGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var store = TestStore.Create(
            d =>
            {
                d.AddSlice<CounterSlice>().AddEffect<LoadEffect>().Use<GatedInitMiddleware>();
                d.AddValidation(sp =>
                {
                    resolved = sp.GetService<object>();
                    return [];
                });
            },
            services => services.AddSingleton(marker).AddSingleton(initGate).AddSingleton(TimeProvider.System));
        List<int> changes = [];

        store.InitializeAsync(new CancellationToken(canceled: true)).IsCanceled.ShouldBeTrue();
        initGate.SetResult();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        using var selection = store.Select(s => s.Get<Counter>().Value, changes.Add);
        store.Dispatch(new Increment());
        (await store.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Reduced);
        store.Restore(new Dictionary<string, object> { ["counter"] = new Counter(7) }, Origin.DevTools);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        await store.DispatchAsync(new Load());
        store.WhenIdleAsync(new CancellationToken(canceled: true)).IsCanceled.ShouldBeTrue();
        store.Time.Advance(TimeSpan.FromHours(1));
        await store.Settled();

        resolved.ShouldBeSameAs(marker);
        selection.Value.ShouldBe(100);
        changes.ShouldBe([1, 2, 7, 100]);
        store.InitialState.Get<Counter>().ShouldBe(new Counter(0));
        store.Json.ShouldNotBeNull();
        store.Processed.Select(c => c.Action).ShouldContain(a => a is StoreInitialized);
    }

    // §15: the store's dispose bounds run on the FakeTimeProvider, so an effect run that ignores its token would hang
    // DisposeAsync forever; TestStore bounds it in real time and names the bound, the likely causes and the last actions.
    // The outer 30 s bound turns a missing bound into a TimeoutException instead of a hung run.
    [Fact]
    public async Task TestStore_DisposeWithHungEffectRun_FailsWithinWallClockBound()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outer = TimeSpan.FromSeconds(30);
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var store = TestStore.Create(d => d.AddSlice<CounterSlice>().AddEffect<HangEffect>(), s => s.AddSingleton(gate));
            store.WallClockTimeout.ShouldBe(TimeSpan.FromSeconds(10));
            store.WallClockTimeout = TimeSpan.FromMilliseconds(200);
            for (var i = 0; i < 5; i++)
            {
                await store.DispatchAsync(new Increment());
            }

            await store.DispatchAsync(new Hang());

            var settled = await Should.ThrowAsync<DuckyAssertionException>(() => store.Settled().WaitAsync(outer, cancellationToken));
            settled.Message.ShouldContain("00:00:00.2000000");
            var elapsed = Stopwatch.StartNew();
            var thrown = await Should.ThrowAsync<DuckyAssertionException>(
                () => store.DisposeAsync().AsTask().WaitAsync(outer, cancellationToken));
            elapsed.Stop();

            elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
            thrown.InnerException.ShouldBeOfType<TimeoutException>();
            thrown.Message.ShouldContain("00:00:00.2000000");
            thrown.Message.ShouldContain("ignores cancellation");
            thrown.Message.ShouldContain($"{store.Processed[^2].ActionType}, {store.Processed[^1].ActionType}.");
            thrown.Message.ShouldContain(store.Processed[^5].ActionType);
            thrown.Message.ShouldNotContain(store.Processed[0].ActionType);
        }
        finally
        {
            gate.TrySetResult();
        }
    }

    public static TheoryData<ServiceLifetime> Lifetimes => new() { ServiceLifetime.Singleton, ServiceLifetime.Scoped };

    // Dispose and DisposeAsync dispose the real store, whichever container owns it: a later dispatch completes Disposed.
    [Theory]
    [MemberData(nameof(Lifetimes))]
    public async Task TestStore_Dispose_DisposesStore(ServiceLifetime lifetime)
    {
        var store = TestStore.Create(d => d.AddSlice<CounterSlice>().Lifetime = lifetime);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispose();
        (await store.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Disposed);

        var other = TestStore.Create(d => d.AddSlice<CounterSlice>().Lifetime = lifetime);
        await other.InitializeAsync(TestContext.Current.CancellationToken);
        await other.DisposeAsync();
        (await other.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Disposed);
    }

    [Fact]
    public void TestStore_Create_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => TestStore.Create(null!)).ParamName.ShouldBe("configure");
        Should.Throw<ArgumentNullException>(() => TestStore.Create(_ => { }, null, null!)).ParamName.ShouldBe("failureTypes");
    }

    // §15 names a hung middleware init as a likely cause: StoreInitialized is never reduced, so Processed is empty and
    // the message says so instead of leaving the slot blank.
    [Fact]
    public async Task TestStore_DisposeWithHungMiddlewareInit_NamesNoActions()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var store = TestStore.Create(d => d.AddSlice<CounterSlice>().Use<HangInitMiddleware>(), s => s.AddSingleton(gate));
            store.WallClockTimeout = TimeSpan.FromMilliseconds(200);
            store.Dispatch(new Increment());

            var thrown = await Should.ThrowAsync<DuckyAssertionException>(
                () => store.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

            store.Processed.ShouldBeEmpty();
            thrown.Message.ShouldContain("Last processed actions: (none).");
        }
        finally
        {
            gate.TrySetResult();
        }
    }
}
