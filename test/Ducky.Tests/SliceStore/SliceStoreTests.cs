using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Ducky.Tests.SliceStoreFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.4 and §12 (API-01, ADR-0010): the SliceStore facade; INV-30, INV-11 (a Set inside a run is scoped to it).
public sealed class SliceStoreTests
{
    public static TheoryData<ServiceLifetime> Lifetimes => new() { ServiceLifetime.Singleton, ServiceLifetime.Scoped };

    // AddSlice<CartStore>() makes CartStore injectable, resolved to the instance the store owns; each store owns its own.
    [Fact]
    public async Task SliceStore_InjectedInstance_IsStoreOwnedSlice()
    {
        var services = new ServiceCollection().AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<CartStore>();
        });
        await using var provider = services.BuildServiceProvider();
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var cart = first.ServiceProvider.GetRequiredService<CartStore>();
        var store = first.ServiceProvider.GetRequiredService<IStore>();

        store.Slices.ShouldHaveSingleItem().ShouldBeSameAs(cart);
        first.ServiceProvider.GetRequiredService<CartStore>().ShouldBeSameAs(cart);
        second.ServiceProvider.GetRequiredService<CartStore>().ShouldNotBeSameAs(cart);

        (await cart.AddAsync("milk")).ShouldBe(DispatchResult.Reduced);

        cart.State.ShouldBeSameAs(store.State.Get<CartState>());
        cart.State.Items.ShouldBe(["milk"]);
        cart.Count.ShouldBe(1);
        second.ServiceProvider.GetRequiredService<CartStore>().Count.ShouldBe(0);
    }

    // A mutator that returns the same reference commits nothing and notifies nobody, from Set and SetAsync alike.
    [Fact]
    public async Task SliceStore_SameReference_NoCommit()
    {
        var recorder = new Recorder("r", []);
        var cart = new CartStore();
        var store = new DuckyStore([cart], NullLogger.Instance, middleware: () => [recorder]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        List<CartState> notified = [];
        using var selection = store.Select(s => s.Get<CartState>(), notified.Add);
        var before = store.State;
        var notifies = 0;
        store.Dispatcher.BeforeNotifyHook = () => notifies++;
        recorder.Contexts.Clear();

        cart.Touch();
        (await cart.TouchAsync()).ShouldBe(DispatchResult.Reduced);

        store.State.ShouldBeSameAs(before);
        notified.ShouldBeEmpty();
        notifies.ShouldBe(0);
        var reduced = recorder.Contexts.DistinctBy(c => c.Id).ToList();
        reduced.Select(c => c.ActionType).ShouldBe(["cart/Touch", "cart/TouchAsync"]);
        reduced.ShouldAllBe(c => c.ChangedKeys.Count == 0 && ReferenceEquals(c.State, c.PreviousState));
    }

    // A SliceStore created with new was never attached by a store: State, Set and SetAsync throw DUCKY351 synchronously.
    [Fact]
    public void SliceStore_StateBeforeAttach_Throws()
    {
        var cart = new CartStore();
        Action[] calls =
        [
            () => _ = cart.State,
            () => _ = cart.Count,
            () => cart.Add("milk"),
            () => _ = cart.AddAsync("milk"),
        ];

        foreach (var call in calls)
        {
            var thrown = Should.Throw<DuckyConfigurationException>(call);
            var error = thrown.Errors.ShouldHaveSingleItem();
            error.Code.ShouldBe("DUCKY351");
            error.Message.ShouldContain("Ducky.Tests.SliceStoreFixtures.CartStore");
        }
    }

    // An effect constructor may inject a SliceStore: the store creates the effect on its first use and the injected
    // instance is the store-owned one, whose Set calls from the run reduce on that store (§6.6, §12).
    [Theory]
    [MemberData(nameof(Lifetimes))]
    public async Task Effect_InjectsSliceStore_Resolves(ServiceLifetime lifetime)
    {
        var journal = new EffectJournal();
        var services = new ServiceCollection().AddSingleton(journal).AddDucky(d =>
        {
            d.Lifetime = lifetime;
            d.AddSlice<CartStore>();
            d.AddEffect<BuyEffect>();
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        var cart = scope.ServiceProvider.GetRequiredService<CartStore>();

        (await store.DispatchAsync(new Buy("tea"))).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        journal.Handled.ShouldHaveSingleItem().Effect.ShouldBeOfType<BuyEffect>().Cart.ShouldBeSameAs(cart);
        cart.State.Items.ShouldBe(["tea", "tea!"]);
    }

    // Non-normative: Set's action type is {key}/{name}; outside an effect run it is Local on a new chain, inside a run of
    // the owning store it is Effect on the run's chain (here synchronous children of the trigger, since the run's prefix
    // dispatches them on the drainer).
    [Fact]
    public async Task SliceStore_Set_ActionTypeAndOriginFollowTheRun()
    {
        var recorder = new Recorder("r", []);
        var journal = new EffectJournal();
        var cart = new CartStore();
        var store = new DuckyStore([cart], NullLogger.Instance, middleware: () => [recorder], effects: () => [(new BuyEffect(cart, journal), false)]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        recorder.Contexts.Clear();

        cart.Add("tea");
        (await store.DispatchAsync(new Buy("milk"))).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        var contexts = recorder.Contexts.DistinctBy(c => c.Id).ToList();
        var buy = contexts.Single(c => c.Action is Buy);
        contexts.Where(c => c.Action is not Buy).Select(c => (c.ActionType, c.Origin, c.Depth, c.CorrelationId == buy.CorrelationId)).ShouldBe(
        [
            ("cart/Add", Origin.Local, 0, false),
            ("cart/Add", Origin.Effect, 1, true),
            ("cart/AddAsync", Origin.Effect, 1, true),
        ]);
        cart.State.Items.ShouldBe(["tea", "milk", "milk!"]);
    }

    // Non-normative: a null mutator is a programmer error, thrown synchronously (§7 rule 4).
    [Fact]
    public void SliceStore_NullMutator_ThrowsArgumentNull()
    {
        var cart = new CartStore();
        _ = new DuckyStore([cart], NullLogger.Instance);

        Should.Throw<ArgumentNullException>(cart.SetNull);
        Should.Throw<ArgumentNullException>(() => _ = cart.SetNullAsync());
    }

    // Non-normative: Set and SetAsync materialize the store first, so a constructor's cached DUCKY353 is rethrown
    // synchronously, as from IStore.Dispatch (§6.6, §7 rule 4).
    [Fact]
    public async Task SliceStore_Set_MaterializationFailed_RethrowsSynchronously()
    {
        var services = new ServiceCollection().AddSingleton(new EffectJournal()).AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Scoped;
            d.AddSlice<CartStore>();
            d.AddEffect<ThrowingEffect>();
        });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var cart = scope.ServiceProvider.GetRequiredService<CartStore>();

        var thrown = Should.Throw<DuckyConfigurationException>(() => cart.Add("milk"));

        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY353");
        Should.Throw<DuckyConfigurationException>(() => _ = cart.AddAsync("milk")).ShouldBeSameAs(thrown);
    }
}
