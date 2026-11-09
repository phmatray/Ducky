// A memoized selector: its projector runs again only when one of its inputs changed by
// reference, so a basket change or typing elsewhere never re-filters the shelf.
using System.Collections.Immutable;
using Ducky.Playground.Shop.Search;

namespace Ducky.Playground.Shop.Catalogue;

public static class ShelfSelectors
{
    // The ids on the shelf, in display order; each subscriber caches through Create().
    // Cards select their own duck by id, so a stock change re-renders one card.
    public static readonly SelectorDef<ImmutableArray<string>> VisibleIds =
        Selector.Define(
            static state => state.Get<CatalogueState>().Ducks,
            static state => state.Get<SearchState>().Hits,
            static state => state.Get<Filters>(),
            static (ducks, hits, filters) =>
            {
                IEnumerable<Duck> shelf = ducks.Items;
                if (hits is { } searchHits)
                {
                    var hitIds = searchHits.ToHashSet(StringComparer.Ordinal);
                    shelf = shelf.Where(duck => hitIds.Contains(duck.Id));
                }
                if (filters.Colour is { } colour)
                {
                    shelf = shelf.Where(duck => duck.Colour == colour);
                }
                if (filters.InStockOnly)
                {
                    shelf = shelf.Where(static duck => duck.InStock);
                }
                shelf = filters.Sort switch
                {
                    ShelfSort.PriceLowToHigh => shelf.OrderBy(static duck => duck.Price),
                    ShelfSort.PriceHighToLow =>
                        shelf.OrderByDescending(static duck => duck.Price),
                    ShelfSort.Name =>
                        shelf.OrderBy(static duck => duck.Name, StringComparer.Ordinal),
                    _ => shelf,
                };
                return shelf.Select(static duck => duck.Id).ToImmutableArray();
            });

    // The same ids in the same order count as equal, so the shelf keeps its render.
    public static readonly IEqualityComparer<ImmutableArray<string>> SameIds =
        new SameIdsComparer();

    private sealed class SameIdsComparer : IEqualityComparer<ImmutableArray<string>>
    {
        public bool Equals(ImmutableArray<string> x, ImmutableArray<string> y) =>
            x.SequenceEqual(y, StringComparer.Ordinal);

        public int GetHashCode(ImmutableArray<string> obj) => obj.Length;
    }
}
