#region shelf-selectors
using System.Collections.Immutable;
using Ducky;

namespace DuckShop;

public static class ShelfSelectors
{
    // The ducks the shelf shows: the catalogue, kept to the search hits when there are any,
    // cheapest first. Memoized: the projector runs again only when the ducks or the hits
    // changed, and returns the same list otherwise.
    public static readonly SelectorDef<ImmutableList<Duck>> VisibleDucks = Selector.Define(
        state => state.Get<CatalogueState>().Ducks,
        state => state.Get<SearchState>().Hits,
        (ducks, hits) => ducks
            .Where(duck => hits is null || hits.Contains(duck.Id))
            .OrderBy(duck => duck.Price)
            .ToImmutableList());
}
#endregion
