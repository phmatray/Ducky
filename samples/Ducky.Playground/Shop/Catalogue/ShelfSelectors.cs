using System.Collections.Immutable;
using Ducky.Playground.Shop.Search;

namespace Ducky.Playground.Shop.Catalogue;

// Memoized selectors: a projector runs again only when one of its inputs changed (by reference), so typing in a field
// elsewhere, or a cart change, never re-filters the shelf. Each subscriber gets its own cache through Create().
public static class ShelfSelectors
{
    // The ids on the shelf, in display order. The cards select their own duck by id, so a stock change re-renders one card.
    public static readonly SelectorDef<ImmutableArray<string>> Visible = Selector.Define(
        static s => s.Get<CatalogueState>().Ducks,
        static s => s.Get<SearchState>().Hits,
        static s => s.Get<Filters>(),
        static (ducks, hits, filters) =>
        {
            IEnumerable<Duck> shelf = ducks.Items;
            if (hits is { } found)
            {
                var matching = found.ToHashSet(StringComparer.Ordinal);
                shelf = shelf.Where(duck => matching.Contains(duck.Id));
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
                ShelfSort.PriceHighToLow => shelf.OrderByDescending(static duck => duck.Price),
                ShelfSort.Name => shelf.OrderBy(static duck => duck.Name, StringComparer.Ordinal),
                _ => shelf,
            };
            return shelf.Select(static duck => duck.Id).ToImmutableArray();
        });

    // Equal when the same ids come back in the same order: the shelf then keeps its render.
    public static readonly IEqualityComparer<ImmutableArray<string>> SameIds = new IdsComparer();

    private sealed class IdsComparer : IEqualityComparer<ImmutableArray<string>>
    {
        public bool Equals(ImmutableArray<string> x, ImmutableArray<string> y) => x.SequenceEqual(y, StringComparer.Ordinal);

        public int GetHashCode(ImmutableArray<string> obj) => obj.Length;
    }
}
