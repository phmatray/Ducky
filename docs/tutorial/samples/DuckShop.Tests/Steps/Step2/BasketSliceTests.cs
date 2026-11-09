using Ducky.Testing;

namespace DuckShop.Tests.Steps.Step2;

public sealed class BasketSliceTests
{
    // Step 2's "Run it": the store holds an empty basket under the key derived from the
    // class name, and the final slice starts from the same state.
    [Fact]
    public async Task Step2_NewStore_HoldsAnEmptyBasket()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<BasketSlice>());
        await using var final = TestStore.Create(ducky => ducky.AddSlice<DuckShop.BasketSlice>());

        store.State.Get<BasketState>().Lines.ShouldBeEmpty();
        store.State.Get<BasketState>().Count.ShouldBe(0);
        store.Slices.ShouldHaveSingleItem().Key.ShouldBe("basket");
        final.State.Get<BasketState>().Lines.ShouldBeEmpty();
    }
}
