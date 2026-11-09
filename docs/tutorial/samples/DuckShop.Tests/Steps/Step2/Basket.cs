using Ducky;

namespace DuckShop.Tests.Steps.Step2;

// The basket slice as step 2 leaves it: an initial state, no reducer yet (step 3 adds them).
#region step2-basket-slice
public sealed class BasketSlice : Slice<BasketState>
{
    // What the basket holds before anything happens: no line.
    protected override BasketState Initial => new([]);
}
#endregion
