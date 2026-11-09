using System.Collections.Immutable;
using Ducky.Playground.Shop.Api;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Catalogue;

// The ducks, normalized: EntityState keeps insertion order and finds a duck by id without scanning.
public sealed record CatalogueState(EntityState<string, Duck> Ducks, bool Loading, string? Error)
{
    // The same Catalogue when the collection is the same instance, so an update that changes nothing commits nothing.
    public CatalogueState With(EntityState<string, Duck> ducks) => ReferenceEquals(ducks, Ducks) ? this : this with { Ducks = ducks };
}

[ActionType("catalogue/loading")]
public sealed record CatalogueLoading;

[ActionType("catalogue/loaded")]
public sealed record CatalogueLoaded(ImmutableList<Duck> Ducks);

[ActionType("catalogue/failed")]
public sealed record CatalogueFailed(string Error);

// The warehouse feed. A delta that would take a duck below zero is malformed: its reducer throws (Duck.Restocked).
[DuckyAction]
[ActionType("warehouse/stock-adjusted")]
public sealed record AdjustStock(string DuckId, int Delta);

public sealed class CatalogueSlice : Slice<CatalogueState>
{
    public CatalogueSlice()
    {
        On<CatalogueLoading>(state => state with { Loading = true, Error = null });
        On<CatalogueLoaded>((state, action) => new(state.Ducks.SetAll(action.Ducks), Loading: false, Error: null));
        On<CatalogueFailed>((state, action) => state with { Loading = false, Error = action.Error });
        On<AdjustStock>((state, action) => state.With(state.Ducks.Update(action.DuckId, duck => duck.Restocked(action.Delta))));
        On<OrderPlaced>((state, action) => state.With(action.Lines.Aggregate(
            state.Ducks,
            static (ducks, line) => ducks.Update(line.DuckId, duck => duck with { Stock = Math.Max(0, duck.Stock - line.Quantity) }))));
    }

    public override string Key => "catalogue";

    protected override CatalogueState Initial => new(EntityState<string, Duck>.Empty, Loading: true, Error: null);
}

// Runs once, when the store is initialized (after hydration): loads the ducks from the fake API.
public sealed class LoadCatalogueEffect(ShopApi api) : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken) =>
        context.Run(
            started: new CatalogueLoading(),
            work: api.GetCatalogueAsync,
            succeeded: ducks => new CatalogueLoaded(ducks),
            failed: exception => new CatalogueFailed(exception.Message),
            cancellationToken);
}
