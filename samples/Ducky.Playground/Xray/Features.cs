using System.Collections.Immutable;

namespace Ducky.Playground.Xray;

public sealed record SourceRef(string Path, string Focus);

public sealed record GuideLink(string Title, string Href);

// One X-ray tag: what it names, what the drawer says about it, the guides behind it and the app's own files that do it.
// Focus is a piece of text the drawer marks with a lamp in the gutter and scrolls to. Order is the region's place in the
// page's reading order, top bar down to the tape: X-ray reveals the tags and outlines in that order.
public sealed record Feature(string Id, string Tag, string Title, string Summary, ImmutableArray<GuideLink> Guides, ImmutableArray<SourceRef> Files, int Order);

public static class Features
{
    private static readonly GuideLink _effects = new("Effects and concurrency policies", "../docs/guides/core/effects.html");
    private static readonly GuideLink _threading = new("Threading contract", "../docs/threading.html");
    private static readonly GuideLink _selectors = new("Selectors", "../docs/guides/core/selectors.html");

    public static readonly ImmutableArray<Feature> All =
    [
        new("search", "Switch", "Live search runs on Switch",
            "Every keystroke dispatches TypeSearch. The search effect's policy is Concurrency.Switch, so a newer keystroke cancels the run in progress, whether it is still waiting out the 250 ms pause on the store's TimeProvider or already waiting for the warehouse. A cancelled run dispatches nothing more, so a slow answer to an old term can never land after the newer one. The tag counts the runs superseded so far.",
            [_effects, _threading],
            [new("Shop/Search/SearchEffect.cs", "Concurrency.Switch"), new("Shop/Search/SearchState.cs", "Superseded =>"), new("Shop/Search/SearchBar.razor", "TypeSearch")],
            Order: 1),
        new("catalogue", "EntityState + selectors", "An EntityState shelf behind memoized selectors",
            "The 36 ducks live in an EntityState<string, Duck>: insertion order kept, lookup by id without scanning, and the same instance back when an update changes nothing. The shelf selects a memoized SelectorDef over the catalogue, the search hits and the filters: its projector runs again only when one of those changed by reference, and a comparer keeps the shelf's render when the same ids come back. Each card selects its own duck by id, so a stock change re-renders one card.",
            [new("EntityState", "../docs/guides/core/entity-state.html"), _selectors],
            [new("Shop/Catalogue/CatalogueState.cs", "EntityState<string, Duck> Ducks"), new("Shop/Catalogue/ShelfSelectors.cs", "Selector.Define"), new("Shop/Catalogue/Shelf.razor", "SameIds"), new("Shop/Catalogue/DuckCard.razor", "Ducks.Find(DuckId)")],
            Order: 3),
        new("veto", "Middleware veto", "A MayDispatch veto keeps the eleventh duck out",
            "Before the store reduces an action from a component or an effect, it asks each middleware's MayDispatch. QuantityLimitMiddleware refuses an AddToCart past ten of one duck or past the stock: the dispatch completes DispatchResult.Vetoed and nothing is reduced. The card awaited that result, explains it with the same rule, and logs the veto on the inspector tape.",
            [_threading],
            [new("Shop/Cart/ShopRules.cs", "MayDispatch"), new("Shop/Catalogue/DuckCard.razor", "DispatchResult.Vetoed"), new("Program.cs", "Use<QuantityLimitMiddleware>")],
            Order: 3),
        new("basket", "Persist + SyncAcrossTabs", "The basket survives a reload and follows your other tabs",
            "The basket is a persisted slice: written to localStorage after each change and restored before the store reports ready, so a reload keeps it. Version 2 changed the stored shape; an entry written by version 1 runs the migration step over JsonNode before it is read (plant one below and watch it come back). SyncAcrossTabs restores what another tab of this origin wrote: open the shop twice and the inspector logs CrossTab restores.",
            [new("Persistence", "../docs/guides/blazor/persistence.html"), new("Cross-tab sync", "../docs/guides/blazor/cross-tab.html")],
            [new("Shop/Cart/Cart.cs", "PersistCart"), new("Program.cs", "PersistCart"), new("Shop/Cart/CartPanel.razor", "PlantVersionOneAsync")],
            Order: 1),
        new("checkout", "Exhaust", "Checkout runs on Exhaust and fails gracefully",
            "CheckOut starts the checkout effect, whose policy is Concurrency.Exhaust: while a payment runs, a further CheckOut is still reduced (the tag counts the requests) but starts no run. The store logs and counts each drop, and the inspector shows it with a beak lamp. A declined payment throws out of the handler: the store dispatches EffectFailed naming the action, the checkout slice reduces it into the message you see, and the store keeps running.",
            [_effects],
            [new("Shop/Checkout/CheckoutEffect.cs", "Concurrency.Exhaust"), new("Shop/Checkout/CheckoutState.cs", "On<EffectFailed>"), new("Shop/Api/ShopApi.cs", "throw new PaymentDeclinedException")],
            Order: 2),
        new("orders", "Polling", "Order tracking polls until it is delivered",
            "Tracking is a Ducky.Reactive pipeline, and the state stream drives its timer. While the current order is on its way it polls the status every 1.5 seconds on the injected TimeProvider; once the order is delivered, Tracking turns null and Switch disposes the timer, so polling stops by itself. The order is persisted too: reload mid-delivery and the same stream picks it up.",
            [new("Reactive effects", "../docs/guides/reactive/recipes.html")],
            [new("Shop/Orders/OrderTrackingEffect.cs", "Observable.Interval"), new("Shop/Orders/OrdersState.cs", "Tracking =>"), new("Shop/Orders/OrderTracker.razor", "OrdersState")],
            Order: 3),
        new("profile", "Produce", "The delivery address is edited with Produce",
            "The profile is a nested record, edited in a reducer with Ducky.Draft: draft.Address.City = value, and Produce returns a new immutable Profile. Untouched branches stay the same instances, which X-ray reports after each edit, and typing a value that is already there returns the original record, so nothing is committed and no other component renders.",
            [new("Ducky.Draft", "../docs/guides/draft/produce.html")],
            [new("Shop/Account/Profile.cs", "state.Produce"), new("Shop/Account/ProfilePanel.razor", "ReferenceEquals")],
            Order: 4),
        new("reducer-failed", "ReducerFailed", "A throwing reducer commits nothing",
            "A malformed restock makes the catalogue's reducer throw. The store discards every slice's result for that action, the restock log's included (one action, one atomic commit), completes the dispatch Failed and dispatches ReducerFailed, which the incidents slice reduces. Nothing else stops: a valid restock right after goes through.",
            [_threading],
            [new("Shop/Catalogue/Duck.cs", "Restocked"), new("Shop/Warehouse/RestockLog.cs", "On<ReducerFailed>"), new("Shop/Warehouse/WarehouseDesk.razor", "DispatchResult.Failed")],
            Order: 5),
        new("slice-store", "SliceStore", "X-ray itself is a SliceStore",
            "The X-ray switch calls Xray.Toggle() on a SliceStore, a slice with methods. The store reduces the change like any action (the inspector names it xray/Toggle), persists it so X-ray remembers its state, and re-renders the components that selected it. The code drawer is another SliceStore.",
            [new("SliceStore", "../docs/guides/core/slice-store.html")],
            [new("Xray/XrayStore.cs", "public void Toggle()"), new("Layout/TopBar.razor", "Xray.Toggle()")],
            Order: 0),
        new("renders", "Render count", "A store change renders only what selected it",
            "Each component shows how many times it rendered. A store change re-renders only the components whose selected value changed, compared on the renderer with the value they last rendered; a component also renders after its own event handlers, like any ComponentBase. Watch the counters: changing a filter re-renders the filters and the shelf, not the basket; adding a duck re-renders its card and the basket, not the shelf or the other cards.",
            [_selectors],
            [new("Xray/ShopComponent.cs", "RenderCount"), new("Shop/Catalogue/DuckCard.razor", "Select<"), new("Shop/Catalogue/Shelf.razor", "Select(")],
            Order: 2),
        new("audit", "Audit middleware", "The inspector is a middleware",
            "The tape is fed by AuditMiddleware's AfterReduce, which sees every action the store processed, whatever its origin: Local, Effect, Hydration, CrossTab or System. Drops never reach a middleware, so a MeterListener reads the store's own drop counters; vetoes come from the DispatchResult the components awaited. Open a row for its payload and the state it changed.",
            [_threading],
            [new("Inspector/AuditMiddleware.cs", "AfterReduce"), new("Inspector/DropMeter.cs", "ducky.effect.dropped"), new("Inspector/Tape.cs", "EffectRunDropped")],
            Order: 6),
    ];

    public static Feature? Find(string? id) => All.FirstOrDefault(feature => feature.Id == id);
}
