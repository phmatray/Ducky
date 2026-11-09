#region basket-persistence
using System.Text.Json.Nodes;
using Ducky;
using Ducky.Blazor;

namespace DuckShop;

public static class BasketPersistence
{
    // Saves the basket to localStorage after each change and restores it when the app
    // starts again.
    public static DuckyBuilder PersistBasket(this DuckyBuilder ducky) =>
        ducky.Persist<BasketSlice>(options =>
        {
            // Version 2 stores lines. An entry saved by version 1 is migrated when it is read.
            options.Version = 2;
            options.Migrate(1, FromVersion1);

            // A change in one tab shows up in the shop's other tabs.
            options.SyncAcrossTabs = true;
        });

    // Version 1 stored a map from duck id to quantity: {"Items":{"classic":2}}.
    // Version 2 stores {"Lines":[{"DuckId":"classic","Quantity":2}]}.
    private static JsonNode FromVersion1(JsonNode version1)
    {
        var lines = new JsonArray();
        foreach (var (duckId, quantity) in version1["Items"]!.AsObject())
        {
            lines.Add(new JsonObject
            {
                ["DuckId"] = duckId,
                ["Quantity"] = quantity!.GetValue<int>(),
            });
        }
        return new JsonObject { ["Lines"] = lines };
    }
}
#endregion
