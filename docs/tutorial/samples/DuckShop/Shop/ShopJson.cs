#region shop-json
using System.Text.Json.Serialization;

namespace DuckShop;

// The JSON code for the shop's types, generated at build time: persistence writes the
// basket through it, and DevTools and logs use it to show actions and states.
// DUCKY006 names any state type or [DuckyAction] type missing here.
[JsonSerializable(typeof(BasketState))]
[JsonSerializable(typeof(CatalogueState))]
[JsonSerializable(typeof(SearchState))]
[JsonSerializable(typeof(AddToBasket))]
[JsonSerializable(typeof(RemoveFromBasket))]
[JsonSerializable(typeof(TypeSearch))]
public sealed partial class ShopJson : JsonSerializerContext;
#endregion
