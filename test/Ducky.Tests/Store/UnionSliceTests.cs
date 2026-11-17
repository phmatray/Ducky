using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests;

// Slice<TState, TAction> (ADR-0051): a union slice reduces every case through Reduce, whether the case is dispatched alone
// or wrapped in the union (#304 unwraps it). Ducky.Tests runs without the generator, so the slice registers its cases by
// hand, as the generator would.
public sealed class UnionSliceTests
{
    [Fact]
    public async Task UnionSlice_ReducesEachCase_DispatchedAloneOrInTheUnion()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<CartSlice>());
        await using var store = (DuckyStore)services.BuildServiceProvider().GetRequiredService<IStore>();

        await store.DispatchAsync(new AddItem("apple"));
        await store.DispatchAsync(new CartAction(new AddItem("pear")));
        store.State.Get<Cart>().Items.ShouldBe(["apple", "pear"]);

        await store.DispatchAsync(new RemoveItem("apple"));
        store.State.Get<Cart>().Items.ShouldBe(["pear"]);

        await store.DispatchAsync(new CartAction(new Clear()));
        store.State.Get<Cart>().Items.ShouldBeEmpty();
    }

    [Fact]
    public void UnionSlice_WithoutRegisteredCases_HandlesNothing()
    {
        var slice = new UnregisteredSlice();
        slice.Freeze();

        slice.CanHandle(typeof(AddItem)).ShouldBeFalse();
        slice.Reduce(new Cart([])).Items.ShouldBeEmpty();
    }

    internal sealed record Cart(IReadOnlyList<string> Items);

    internal sealed record AddItem(string Name);

    internal sealed record RemoveItem(string Name);

    internal sealed record Clear;

    internal union CartAction(AddItem, RemoveItem, Clear);

    internal sealed class CartSlice : Slice<Cart, CartAction>
    {
        protected override Cart Initial => new([]);

        protected override Cart Reduce(Cart state, CartAction action) => action switch
        {
            AddItem a => new([.. state.Items, a.Name]),
            RemoveItem r => new([.. state.Items.Where(i => i != r.Name)]),
            Clear => new([]),
        };

        protected override void RegisterCases()
        {
            On<AddItem>((s, a) => Reduce(s, new CartAction(a)));
            On<RemoveItem>((s, a) => Reduce(s, new CartAction(a)));
            On<Clear>((s, a) => Reduce(s, new CartAction(a)));
        }
    }

    internal sealed class UnregisteredSlice : Slice<Cart, CartAction>
    {
        protected override Cart Initial => new([]);

        // Reached only through Reduce(Cart) below: with no case registered, the store never calls it.
        internal Cart Reduce(Cart state) => Reduce(state, new CartAction(new Clear()));

        protected override Cart Reduce(Cart state, CartAction action) => state;
    }
}
