using System.Collections.Immutable;

namespace Ducky.Playground.Xray;

// A file the drawer shows for a feature. Focus is text, not a line number: the drawer
// marks the first line of code containing it, so the mark follows the code as it moves.
public sealed record SourceRef(string Path, string Focus);

public sealed record GuideLink(string Title, string Href);

// One X-ray tag: what it names, what the drawer says about it, the guides behind it and
// the app's own files that do it. Order is the region's place in reading order, top bar
// down to the tape: X-ray reveals the tags and outlines in that order.
public sealed record Feature(
    string Id,
    string Tag,
    string Title,
    string Summary,
    ImmutableArray<GuideLink> Guides,
    ImmutableArray<SourceRef> Files,
    int Order);

public static class Features
{
    private static readonly GuideLink _effects =
        new("Effects and concurrency policies", "../docs/guides/core/effects.html");

    private static readonly GuideLink _threading =
        new("Threading contract", "../docs/threading.html");

    private static readonly GuideLink _selectors =
        new("Selectors", "../docs/guides/core/selectors.html");

    public static readonly ImmutableArray<Feature> All =
    [
        new(
            Id: "search",
            Tag: "Switch",
            Title: "Live search runs on Switch",
            Summary:
                "Every keystroke dispatches TypeSearch. The search effect's policy is "
                + "Concurrency.Switch, so a newer keystroke cancels the run in progress, "
                + "whether it is still waiting out the 250 ms pause on the store's "
                + "TimeProvider or already waiting for the warehouse. A cancelled run "
                + "dispatches nothing more, so a slow answer to an old term can never "
                + "land after the newer one. The tag counts the runs superseded so far.",
            Guides: [_effects, _threading],
            Files:
            [
                Source("Shop/Search/SearchEffect.cs", "Concurrency.Switch"),
                Source("Shop/Search/SearchState.cs", "Superseded =>"),
                Source("Shop/Search/SearchBar.razor", "TypeSearch"),
            ],
            Order: 1),
        new(
            Id: "catalogue",
            Tag: "EntityState + selectors",
            Title: "An EntityState shelf behind memoized selectors",
            Summary:
                "The 36 ducks live in an EntityState<string, Duck>: insertion order "
                + "kept, lookup by id without scanning, and the same instance back when "
                + "an update changes nothing. The shelf selects a memoized SelectorDef "
                + "over the catalogue, the search hits and the filters: its projector "
                + "runs again only when one of those changed by reference, and a "
                + "comparer keeps the shelf's render when the same ids come back. Each "
                + "card selects its own duck by id, so a stock change re-renders one "
                + "card.",
            Guides:
            [
                new("EntityState", "../docs/guides/core/entity-state.html"),
                _selectors,
            ],
            Files:
            [
                Source(
                    "Shop/Catalogue/CatalogueState.cs",
                    "EntityState<string, Duck> Ducks"),
                Source("Shop/Catalogue/ShelfSelectors.cs", "Selector.Define"),
                Source("Shop/Catalogue/Shelf.razor", "SameIds"),
                Source("Shop/Catalogue/DuckCard.razor", "Ducks.Find(DuckId)"),
            ],
            Order: 3),
        new(
            Id: "veto",
            Tag: "Middleware veto",
            Title: "A MayDispatch veto keeps the eleventh duck out",
            Summary:
                "Before the store reduces an action from a component or an effect, it "
                + "asks each middleware's MayDispatch. QuantityLimitMiddleware refuses "
                + "an AddToBasket past ten of one duck or past the stock: the dispatch "
                + "completes DispatchResult.Vetoed and nothing is reduced. The card "
                + "awaited that result, explains it with the same rule, and logs the "
                + "veto on the inspector tape.",
            Guides: [_threading],
            Files:
            [
                Source("Shop/Basket/ShopRules.cs", "MayDispatch"),
                Source("Shop/Catalogue/DuckCard.razor", "DispatchResult.Vetoed"),
                Source("Program.cs", "Use<QuantityLimitMiddleware>"),
            ],
            Order: 3),
        new(
            Id: "basket",
            Tag: "Persist + SyncAcrossTabs",
            Title: "The basket survives a reload and follows your other tabs",
            Summary:
                "The basket is a persisted slice: written to localStorage after each "
                + "change and restored before the store reports ready, so a reload keeps "
                + "it. Version 2 changed the stored shape; an entry written by version 1 "
                + "runs the migration step over JsonNode before it is read (plant one "
                + "below and watch it come back). SyncAcrossTabs restores what another "
                + "tab of this origin wrote: open the shop twice and the inspector logs "
                + "CrossTab restores.",
            Guides:
            [
                new("Persistence", "../docs/guides/blazor/persistence.html"),
                new("Cross-tab sync", "../docs/guides/blazor/cross-tab.html"),
            ],
            Files:
            [
                Source("Shop/Basket/Basket.cs", "PersistBasket"),
                Source("Program.cs", "PersistBasket"),
                Source("Shop/Basket/BasketPanel.razor", "PlantVersionOneAsync"),
            ],
            Order: 1),
        new(
            Id: "checkout",
            Tag: "Exhaust",
            Title: "Checkout runs on Exhaust and fails gracefully",
            Summary:
                "CheckOut starts the checkout effect, whose policy is "
                + "Concurrency.Exhaust: while a payment runs, a further CheckOut is "
                + "still reduced (the tag counts the requests) but starts no run. The "
                + "store logs and counts each drop, and the inspector shows it with a "
                + "beak lamp. A declined payment throws out of the handler: the store "
                + "dispatches EffectFailed naming the action, the checkout slice reduces "
                + "it into the message you see, and the store keeps running.",
            Guides: [_effects],
            Files:
            [
                Source("Shop/Checkout/CheckoutEffect.cs", "Concurrency.Exhaust"),
                Source("Shop/Checkout/CheckoutState.cs", "On<EffectFailed>"),
                Source("Shop/Api/ShopApi.cs", "throw new PaymentDeclinedException"),
            ],
            Order: 2),
        new(
            Id: "orders",
            Tag: "Polling",
            Title: "Order tracking polls until it is delivered",
            Summary:
                "Tracking is a Ducky.Reactive pipeline, and the state stream drives "
                + "its timer. While the current order is on its way it polls the status "
                + "every 1.5 seconds on the injected TimeProvider; once the order is "
                + "delivered, Tracking turns null and Switch disposes the timer, so "
                + "polling stops by itself. The order is persisted too: reload "
                + "mid-delivery and the same stream picks it up.",
            Guides: [new("Reactive effects", "../docs/guides/reactive/recipes.html")],
            Files:
            [
                Source("Shop/Orders/OrderTrackingEffect.cs", "Observable.Interval"),
                Source("Shop/Orders/OrdersState.cs", "Tracking =>"),
                Source("Shop/Orders/OrderTracker.razor", "OrdersState"),
            ],
            Order: 3),
        new(
            Id: "profile",
            Tag: "Produce",
            Title: "The delivery address is edited with Produce",
            Summary:
                "The profile is a nested record, edited in a reducer with Ducky.Draft: "
                + "draft.Address.City = value, and Produce returns a new immutable "
                + "Profile. Untouched branches stay the same instances, which X-ray "
                + "reports after each edit, and typing a value that is already there "
                + "returns the original record, so nothing is committed and no other "
                + "component renders.",
            Guides: [new("Ducky.Draft", "../docs/guides/draft/produce.html")],
            Files:
            [
                Source("Shop/Account/Profile.cs", "state.Produce"),
                Source("Shop/Account/ProfilePanel.razor", "ReferenceEquals"),
            ],
            Order: 4),
        new(
            Id: "reducer-failed",
            Tag: "ReducerFailed",
            Title: "A throwing reducer commits nothing",
            Summary:
                "A malformed restock makes the catalogue's reducer throw. The store "
                + "discards every slice's result for that action, the restock log's "
                + "included (one action, one atomic commit), completes the dispatch "
                + "Failed and dispatches ReducerFailed, which the incidents slice "
                + "reduces. Nothing else stops: a valid restock right after goes "
                + "through.",
            Guides: [_threading],
            Files:
            [
                Source("Shop/Catalogue/Duck.cs", "Restocked"),
                Source("Shop/Warehouse/RestockLog.cs", "On<ReducerFailed>"),
                Source("Shop/Warehouse/WarehouseDesk.razor", "DispatchResult.Failed"),
            ],
            Order: 5),
        new(
            Id: "slice-store",
            Tag: "SliceStore",
            Title: "X-ray itself is a SliceStore",
            Summary:
                "The X-ray switch calls Xray.Toggle() on a SliceStore, a slice with "
                + "methods. The store reduces the change like any action (the inspector "
                + "names it xray/Toggle), persists it so X-ray remembers its state, and "
                + "re-renders the components that selected it. The code drawer is "
                + "another SliceStore.",
            Guides: [new("SliceStore", "../docs/guides/core/slice-store.html")],
            Files:
            [
                Source("Xray/XrayStore.cs", "public void Toggle()"),
                Source("Layout/TopBar.razor", "Xray.Toggle()"),
            ],
            Order: 0),
        new(
            Id: "renders",
            Tag: "Render count",
            Title: "A store change renders only what selected it",
            Summary:
                "Each component shows how many times it rendered. A store change "
                + "re-renders only the components whose selected value changed, compared "
                + "on the renderer with the value they last rendered; a component also "
                + "renders after its own event handlers, like any ComponentBase. Watch "
                + "the counters: changing a filter re-renders the filters and the shelf, "
                + "not the basket; adding a duck re-renders its card and the basket, not "
                + "the shelf or the other cards.",
            Guides: [_selectors],
            Files:
            [
                Source("Xray/ShopComponent.cs", "RenderCount"),
                Source("Shop/Catalogue/DuckCard.razor", "Select<"),
                Source("Shop/Catalogue/Shelf.razor", "Select("),
            ],
            Order: 2),
        new(
            Id: "audit",
            Tag: "Audit middleware",
            Title: "The inspector is a middleware",
            Summary:
                "The tape is fed by AuditMiddleware's AfterReduce, which sees every "
                + "action the store processed, whatever its origin: Local, Effect, "
                + "Hydration, CrossTab or System. Drops never reach a middleware, so a "
                + "MeterListener reads the store's own drop counters; vetoes come from "
                + "the DispatchResult the components awaited. Open a row for its payload "
                + "and the state it changed.",
            Guides: [_threading],
            Files:
            [
                Source("Inspector/AuditMiddleware.cs", "AfterReduce"),
                Source("Inspector/DropMeter.cs", "ducky.effect.dropped"),
                Source("Inspector/Tape.cs", "EffectRunDropped"),
            ],
            Order: 6),
    ];

    public static Feature? Find(string? id) =>
        All.FirstOrDefault(feature => feature.Id == id);

    private static SourceRef Source(string path, string focus) => new(path, focus);
}
