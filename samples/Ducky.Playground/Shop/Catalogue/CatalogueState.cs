// The catalogue keeps its ducks in an EntityState: insertion order kept, lookup by id
// without scanning, and the same instance back when an update changes nothing.
using System.Collections.Immutable;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Catalogue;

public sealed record CatalogueState(
    EntityState<string, Duck> Ducks,
    bool Loading,
    string? Error)
{
    // Returning this same state when nothing changed lets the store commit nothing.
    public CatalogueState WithDucks(EntityState<string, Duck> ducks) =>
        ReferenceEquals(ducks, Ducks) ? this : this with { Ducks = ducks };
}

[ActionType("catalogue/loading")]
public sealed record CatalogueLoading;

[ActionType("catalogue/loaded")]
public sealed record CatalogueLoaded(ImmutableList<Duck> Ducks);

[ActionType("catalogue/failed")]
public sealed record CatalogueFailed(string Error);

// The warehouse feed. A delta that would take a duck below zero is malformed, so the
// catalogue's reducer throws on it.
[DuckyAction]
[ActionType("warehouse/stock-adjusted")]
public sealed record AdjustStock(string DuckId, int Delta);

public sealed class CatalogueSlice : Slice<CatalogueState>
{
    public CatalogueSlice()
    {
        On<CatalogueLoading>(state => state with { Loading = true, Error = null });
        On<CatalogueLoaded>((state, action) =>
            new(state.Ducks.SetAll(action.Ducks), Loading: false, Error: null));
        On<CatalogueFailed>((state, action) =>
            state with { Loading = false, Error = action.Error });
        On<AdjustStock>((state, action) => state.WithDucks(
            state.Ducks.Update(action.DuckId, duck => duck.Restocked(action.Delta))));
        On<OrderPlaced>((state, action) => state.WithDucks(
            action.Lines.Aggregate(state.Ducks, static (ducks, line) => ducks.Update(
                line.DuckId,
                duck => duck with { Stock = Math.Max(0, duck.Stock - line.Quantity) }))));
    }

    public override string Key => "catalogue";

    protected override CatalogueState Initial =>
        new(EntityState<string, Duck>.Empty, Loading: true, Error: null);
}

// Starts once the store is initialized, after the persisted slices are restored.
public sealed class LoadCatalogueEffect(ShopApi api) : Effect<StoreInitialized>
{
    public override Task Handle(
        StoreInitialized action,
        EffectContext context,
        CancellationToken cancellationToken) =>
        context.Run(
            started: new CatalogueLoading(),
            work: api.GetCatalogueAsync,
            succeeded: ducks => new CatalogueLoaded(ducks),
            failed: exception => new CatalogueFailed(exception.Message),
            cancellationToken);
}
