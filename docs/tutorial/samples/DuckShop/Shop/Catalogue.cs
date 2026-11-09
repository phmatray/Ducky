#region catalogue-state
using System.Collections.Immutable;
using Ducky;

namespace DuckShop;

// The catalogue as the shop shows it: the ducks, whether they are loading, and the last error.
public sealed record CatalogueState(ImmutableList<Duck> Ducks, bool Loading, string? Error);

// The three moments of a load, dispatched by the effect below.
public sealed record CatalogueLoading;

public sealed record CatalogueLoaded(ImmutableList<Duck> Ducks);

public sealed record CatalogueFailed(string Error);

public sealed class CatalogueSlice : Slice<CatalogueState>
{
    public CatalogueSlice()
    {
        On<CatalogueLoading>(state => state with { Loading = true, Error = null });
        On<CatalogueLoaded>((_, action) =>
            new CatalogueState(action.Ducks, Loading: false, Error: null));
        On<CatalogueFailed>((state, action) =>
            state with { Loading = false, Error = action.Error });
    }

    // Loading from the start: the effect begins as soon as the store is ready.
    protected override CatalogueState Initial => new([], Loading: true, Error: null);
}
#endregion

#region catalogue-effect
// Runs once, when the store has started: StoreInitialized is the store's own first action.
public sealed class LoadCatalogueEffect(ICatalogueApi api) : Effect<StoreInitialized>
{
    public override Task Handle(
        StoreInitialized action,
        EffectContext context,
        CancellationToken cancellationToken) =>
        context.Run(
            started: new CatalogueLoading(),
            work: api.GetDucksAsync,
            succeeded: ducks => new CatalogueLoaded(ducks),
            failed: exception => new CatalogueFailed(exception.Message),
            cancellationToken);
}
#endregion
