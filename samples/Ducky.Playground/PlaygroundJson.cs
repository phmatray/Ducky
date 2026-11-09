using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ducky.Playground.Shop.Account;
using Ducky.Playground.Shop.Basket;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Checkout;
using Ducky.Playground.Shop.Orders;
using Ducky.Playground.Shop.Search;
using Ducky.Playground.Shop.Warehouse;
using Ducky.Playground.Xray;

namespace Ducky.Playground;

// The app's only JSON path: persistence writes the persisted slices through it, and
// DevTools and the inspector show actions and states through it. DUCKY006 reports any
// [DuckyAction] or state type left out; the effects' own actions are listed by hand so
// the inspector can show their payloads too.
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(BasketState))]
[JsonSerializable(typeof(OrdersState))]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(XrayView))]
[JsonSerializable(typeof(DrawerView))]
[JsonSerializable(typeof(CatalogueState))]
[JsonSerializable(typeof(Filters))]
[JsonSerializable(typeof(SearchState))]
[JsonSerializable(typeof(CheckoutState))]
[JsonSerializable(typeof(RestockLog))]
[JsonSerializable(typeof(Incidents))]
[JsonSerializable(typeof(AddToBasket))]
[JsonSerializable(typeof(RemoveOneFromBasket))]
[JsonSerializable(typeof(RemoveBasketLine))]
[JsonSerializable(typeof(PickColour))]
[JsonSerializable(typeof(ToggleInStockOnly))]
[JsonSerializable(typeof(SortShelf))]
[JsonSerializable(typeof(AdjustStock))]
[JsonSerializable(typeof(CatalogueLoading))]
[JsonSerializable(typeof(CatalogueLoaded))]
[JsonSerializable(typeof(CatalogueFailed))]
[JsonSerializable(typeof(TypeSearch))]
[JsonSerializable(typeof(SearchSent))]
[JsonSerializable(typeof(SearchAnswered))]
[JsonSerializable(typeof(CheckOut))]
[JsonSerializable(typeof(PaymentStarted))]
[JsonSerializable(typeof(RigNextPayment))]
[JsonSerializable(typeof(DismissCheckoutProblem))]
[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(OrderStatusPolled))]
[JsonSerializable(typeof(DismissOrder))]
[JsonSerializable(typeof(EditProfile))]
[JsonSerializable(typeof(SetLeaveWithNeighbour))]
[JsonSerializable(typeof(ImmutableArray<string>))]
internal sealed partial class PlaygroundJson : JsonSerializerContext;
