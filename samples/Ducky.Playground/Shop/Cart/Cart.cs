using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Ducky.Blazor;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Cart;

public sealed record CartLine(string DuckId, int Quantity);

public sealed record Basket(ImmutableList<CartLine> Lines)
{
    public int Count => Lines.Sum(static line => line.Quantity);

    public int QuantityOf(string duckId) => Lines.Find(line => line.DuckId == duckId)?.Quantity ?? 0;
}

[DuckyAction]
[ActionType(Type)]
public sealed record AddToCart(string DuckId)
{
    public const string Type = "cart/added";
}

[DuckyAction]
[ActionType("cart/decreased")]
public sealed record RemoveOneFromCart(string DuckId);

[DuckyAction]
[ActionType("cart/line-removed")]
public sealed record RemoveCartLine(string DuckId);

public sealed class CartSlice : Slice<Basket>
{
    public CartSlice()
    {
        On<AddToCart>((state, action) => state.QuantityOf(action.DuckId) is var quantity and > 0
            ? state with { Lines = state.Lines.Replace(state.Lines.Find(line => line.DuckId == action.DuckId)!, new CartLine(action.DuckId, quantity + 1)) }
            : state with { Lines = state.Lines.Add(new CartLine(action.DuckId, 1)) });
        On<RemoveOneFromCart>((state, action) => state.QuantityOf(action.DuckId) switch
        {
            0 => state,
            1 => state with { Lines = state.Lines.RemoveAll(line => line.DuckId == action.DuckId) },
            var quantity => state with { Lines = state.Lines.Replace(state.Lines.Find(line => line.DuckId == action.DuckId)!, new CartLine(action.DuckId, quantity - 1)) },
        });
        On<RemoveCartLine>((state, action) => state.QuantityOf(action.DuckId) == 0
            ? state
            : state with { Lines = state.Lines.RemoveAll(line => line.DuckId == action.DuckId) });
        On<OrderPlaced>(_ => Initial);
    }

    // A persisted slice pins its key: it names the storage entry, ducky-playground:cart (DUCKY012).
    public override string Key => "cart";

    protected override Basket Initial => new([]);
}

public static class CartPersistence
{
    // Every entry of this app is {KeyPrefix}:{slice key}: the basket is ducky-playground:cart.
    public const string KeyPrefix = "ducky-playground";

    // Version 1 of the shop stored the basket as a map, {"Items":{"classic":2}}. Version 2 stores ordered lines. An entry
    // written by version 1 runs the migration over JsonNode before it is deserialized; every write after that is version 2.
    public static DuckyBuilder PersistCart(this DuckyBuilder ducky) => ducky.Persist<CartSlice>(options =>
    {
        options.Version = 2;
        options.Migrate(1, v1 => new JsonObject
        {
            ["Lines"] = new JsonArray([.. v1["Items"]!.AsObject().Select(static item => (JsonNode)new JsonObject
            {
                ["DuckId"] = item.Key,
                ["Quantity"] = item.Value!.GetValue<int>(),
            })]),
        });
        // localStorage is shared by the tabs of this origin: a change in one tab is restored in the others.
        options.SyncAcrossTabs = true;
    });
}
