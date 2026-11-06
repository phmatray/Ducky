using System.Collections.Immutable;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests.Core;

#region slice-store
public sealed record CartLine(string Sku, int Quantity);

public sealed record Cart(ImmutableList<CartLine> Lines);

public sealed record CheckedOut;

public sealed class CartStore : SliceStore<Cart> // key "cart"
{
    public CartStore() => On<CheckedOut>(_ => Initial); // still a slice: it reacts to actions

    protected override Cart Initial => new([]);

    public int Count => State.Lines.Count; // a point-in-time read

    public void Add(CartLine line) => Set(cart => cart with { Lines = cart.Lines.Add(line) }); // DevTools: "cart/Add"

    public void Remove(string sku) => Set(cart => cart with { Lines = cart.Lines.RemoveAll(line => line.Sku == sku) });

    public void Replace(ImmutableList<CartLine> lines) => Set(cart => cart with { Lines = lines });

    public Task<DispatchResult> ClearAsync() => SetAsync(_ => Initial);
}
#endregion

// The snippets of docs/guides/core/slice-store.md.
public sealed class SliceStoreGuideTests
{
    // Private: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every accessible effect of this assembly.
    private static class App
    {
        #region slice-store-effect
        public interface ICartApi
        {
            Task<ImmutableList<CartLine>> LoadAsync(CancellationToken cancellationToken);
        }

        public sealed class LoadCartEffect(ICartApi api, CartStore cart) : Effect<StoreInitialized>
        {
            public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken) =>
                cart.Replace(await api.LoadAsync(cancellationToken));
        }
        #endregion
    }

    // Non-normative (M15-02): the injected CartStore is the store's slice; a selection follows its changes.
    [Fact]
    public async Task SliceStore_InjectedAndSelected_FollowsChanges()
    {
        await using var services = new ServiceCollection()
            .AddDucky(ducky => ducky.AddSlice<CartStore>())
            .BuildServiceProvider();
        var store = services.GetRequiredService<IStore>();

        #region slice-store-use
        var cart = services.GetRequiredService<CartStore>(); // @inject CartStore Cart in a component
        using var lineCount = store.Select((Cart state) => state.Lines.Count);

        cart.Add(new CartLine("duck-yellow", Quantity: 2));
        cart.Add(new CartLine("duck-blue", Quantity: 1));
        cart.Remove("duck-yellow");
        var shown = lineCount.Value;            // 1
        var result = await cart.ClearAsync(); // Reduced, and the cart is empty
        #endregion

        shown.ShouldBe(1);
        result.ShouldBe(DispatchResult.Reduced);
        lineCount.Value.ShouldBe(0);
        cart.Count.ShouldBe(0);
        store.Slices.ShouldContain(cart);
    }

    // Non-normative (M15-02): Set is named {key}/{method}; the slice still reduces its own actions.
    [Fact]
    public async Task SliceStore_SetNamedAfterMethod_ReactsToActions()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CartStore>());
        var cart = store.GetSlice<CartStore>();

        cart.Add(new CartLine("duck-yellow", Quantity: 2));
        var count = cart.Count;
        await store.DispatchAsync(new CheckedOut());

        count.ShouldBe(1);
        cart.Count.ShouldBe(0);
        cart.Key.ShouldBe("cart");
        store.Processed.Where(context => context.Origin == Origin.Local).Select(context => context.ActionType)
            .ShouldBe(["cart/Add", "Ducky.Docs.Tests.Core.CheckedOut"]);
    }

    // Non-normative (M15-02): the StoreInitialized effect loads the cart into the injected SliceStore.
    [Fact]
    public async Task SliceStore_LoadEffect_FillsCartAtInit()
    {
        ImmutableList<CartLine> saved = [new CartLine("duck-yellow", Quantity: 2), new CartLine("duck-blue", Quantity: 1)];
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CartStore>().AddEffect<App.LoadCartEffect>(),
            services => services.AddSingleton<App.ICartApi>(new StubCartApi(saved)));

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.Settled();

        store.State.Get<Cart>().Lines.ShouldBe(saved);
        store.Processed.Single(context => context.ActionType == "cart/Replace").Origin.ShouldBe(Origin.Effect);
        store.Failures.ShouldBeEmpty();
    }

    private sealed class StubCartApi(ImmutableList<CartLine> lines) : App.ICartApi
    {
        public async Task<ImmutableList<CartLine>> LoadAsync(CancellationToken cancellationToken)
        {
            await Task.Yield(); // a real API answers later
            return lines;
        }
    }
}
