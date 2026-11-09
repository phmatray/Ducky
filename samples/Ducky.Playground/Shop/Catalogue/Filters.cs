namespace Ducky.Playground.Shop.Catalogue;

public enum ShelfSort
{
    Featured,
    PriceLowToHigh,
    PriceHighToLow,
    Name,
}

public sealed record Filters(DuckColour? Colour, bool InStockOnly, ShelfSort Sort);

[DuckyAction]
[ActionType("filters/colour-picked")]
public sealed record PickColour(DuckColour? Colour);

[DuckyAction]
[ActionType("filters/in-stock-toggled")]
public sealed record ToggleInStockOnly;

[DuckyAction]
[ActionType("filters/sorted")]
public sealed record SortShelf(ShelfSort Sort);

public sealed class FiltersSlice : Slice<Filters>
{
    public FiltersSlice()
    {
        On<PickColour>((state, action) => state with { Colour = action.Colour });
        On<ToggleInStockOnly>(state => state with { InStockOnly = !state.InStockOnly });
        On<SortShelf>((state, action) => state with { Sort = action.Sort });
    }

    public override string Key => "filters";

    protected override Filters Initial =>
        new(Colour: null, InStockOnly: false, Sort: ShelfSort.Featured);
}
