// The basket is a persisted slice: saved to localStorage after every change, restored
// before the store reports ready, migrated from version 1 and synced across tabs.
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Ducky.Blazor;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Basket;

public sealed record BasketLine(string DuckId, int Quantity);

public sealed record BasketState(ImmutableList<BasketLine> Lines)
{
    public int Count => Lines.Sum(static line => line.Quantity);

    public int QuantityOf(string duckId) => Find(duckId)?.Quantity ?? 0;

    // One line per duck, kept in the order the ducks were added: an existing line
    // changes in place, a new duck goes last, and zero removes the line.
    public BasketState WithQuantity(string duckId, int quantity)
    {
        var lines = (Find(duckId), quantity) switch
        {
            (null, <= 0) => Lines,
            (null, _) => Lines.Add(new BasketLine(duckId, quantity)),
            ({ } line, <= 0) => Lines.Remove(line),
            ({ } line, _) => Lines.Replace(line, line with { Quantity = quantity }),
        };

        // The same instance when nothing changed, so the store commits nothing.
        return ReferenceEquals(lines, Lines) ? this : this with { Lines = lines };
    }

    private BasketLine? Find(string duckId) => Lines.Find(line => line.DuckId == duckId);
}

// Action types start with the slice key, "cart", pinned on BasketSlice below.
[DuckyAction]
[ActionType(Type)]
public sealed record AddToBasket(string DuckId)
{
    public const string Type = "cart/added";
}

[DuckyAction]
[ActionType("cart/decreased")]
public sealed record RemoveOneFromBasket(string DuckId);

[DuckyAction]
[ActionType("cart/line-removed")]
public sealed record RemoveBasketLine(string DuckId);

public sealed class BasketSlice : Slice<BasketState>
{
    // Pinned (DUCKY012): the key names the storage entry, so it keeps the "cart" it was
    // first saved under. A key derived from the class name would orphan saved baskets.
    public const string SliceKey = "cart";

    public BasketSlice()
    {
        On<AddToBasket>((state, action) =>
            state.WithQuantity(action.DuckId, state.QuantityOf(action.DuckId) + 1));
        On<RemoveOneFromBasket>((state, action) =>
            state.WithQuantity(action.DuckId, state.QuantityOf(action.DuckId) - 1));
        On<RemoveBasketLine>((state, action) => state.WithQuantity(action.DuckId, 0));
        On<OrderPlaced>(_ => Initial);
    }

    public override string Key => SliceKey;

    protected override BasketState Initial => new([]);
}

public static class BasketPersistence
{
    // Every storage entry of this app is named {KeyPrefix}:{slice key}.
    public const string KeyPrefix = "ducky-playground";

    public const string StorageKey = $"{KeyPrefix}:{BasketSlice.SliceKey}";

    // Version 2 stores ordered lines. An entry written by version 1 is migrated over
    // JsonNode before it is read, and every write after that is version 2.
    public static DuckyBuilder PersistBasket(this DuckyBuilder ducky) =>
        ducky.Persist<BasketSlice>(options =>
        {
            options.Version = 2;
            options.Migrate(1, FromVersionOne);
            // localStorage is shared by every tab of this origin: a change saved in one
            // tab is restored in the others.
            options.SyncAcrossTabs = true;
        });

    // Version 1 stored a map from duck id to quantity: {"Items":{"classic":2}}.
    private static JsonNode FromVersionOne(JsonNode versionOne)
    {
        JsonNode[] lines =
        [
            .. versionOne["Items"]!.AsObject().Select(static item => new JsonObject
            {
                ["DuckId"] = item.Key,
                ["Quantity"] = item.Value!.GetValue<int>(),
            }),
        ];
        return new JsonObject { ["Lines"] = new JsonArray(lines) };
    }
}
