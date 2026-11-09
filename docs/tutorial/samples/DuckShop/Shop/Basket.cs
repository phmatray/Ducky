#region basket-state
using System.Collections.Immutable;
using Ducky;

namespace DuckShop;

// One line of the basket: which duck, and how many of it.
public sealed record BasketLine(string DuckId, int Quantity);

// The basket's state. A record holding an immutable list: nothing can change it in place,
// so every change to the basket is a new BasketState.
public sealed record BasketState(ImmutableList<BasketLine> Lines)
{
    public int Count => Lines.Sum(line => line.Quantity);

    public int QuantityOf(string duckId) =>
        Lines.Find(line => line.DuckId == duckId)?.Quantity ?? 0;
}
#endregion

#region basket-actions
// What can happen to the basket. [DuckyAction] asks the generator for dispatch helpers:
// store.AddToBasket("classic") dispatches new AddToBasket("classic").
[DuckyAction]
public sealed record AddToBasket(string DuckId);

[DuckyAction]
public sealed record RemoveFromBasket(string DuckId);
#endregion

public sealed class BasketSlice : Slice<BasketState>
{
    #region basket-reducers
    public BasketSlice()
    {
        // A reducer takes the current state and the action, and returns the next state.
        On<AddToBasket>((state, action) =>
            WithQuantity(state, action.DuckId, state.QuantityOf(action.DuckId) + 1));
        On<RemoveFromBasket>((state, action) =>
            WithQuantity(state, action.DuckId, state.QuantityOf(action.DuckId) - 1));
    }

    // A new state with the duck's line set to quantity; zero removes the line. When
    // nothing changes it returns the same state, and the store commits nothing.
    private static BasketState WithQuantity(BasketState state, string duckId, int quantity)
    {
        var line = state.Lines.Find(l => l.DuckId == duckId);
        var lines = (line, quantity) switch
        {
            (null, <= 0) => state.Lines,
            (null, _) => state.Lines.Add(new BasketLine(duckId, quantity)),
            (_, <= 0) => state.Lines.Remove(line),
            _ => state.Lines.Replace(line, line with { Quantity = quantity }),
        };
        return lines == state.Lines ? state : state with { Lines = lines };
    }
    #endregion

    #region basket-key
    // The basket's storage entry is named after this key: pinned, so renaming the class
    // never loses a saved basket (DUCKY012).
    public override string Key => "basket";
    #endregion

    protected override BasketState Initial => new([]);
}
