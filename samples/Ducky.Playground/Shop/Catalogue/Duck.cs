using System.Collections.Immutable;
using System.Globalization;

namespace Ducky.Playground.Shop.Catalogue;

public enum DuckColour
{
    Yellow,
    Black,
    White,
    Red,
    Blue,
    Mint,
    Lilac,
    Green,
}

// An entity of the catalogue's EntityState: IEntity<string> gives it the id the collection indexes.
public sealed record Duck(string Id, string Name, DuckColour Colour, decimal Price, int Stock, ImmutableArray<string> Tags, string Blurb)
    : IEntity<string>
{
    public bool InStock => Stock > 0;

    // Called by a reducer: a delta that would take the stock below zero is a malformed action, and throwing here makes
    // the store report ReducerFailed and commit nothing.
    public Duck Restocked(int delta) => Stock + delta < 0
        ? throw new InvalidOperationException($"{Name} can't go below zero stock ({Stock} {delta:+#;-#;0}).")
        : this with { Stock = Stock + delta };
}

public static class DuckColours
{
    public static string Hex(DuckColour colour) => colour switch
    {
        DuckColour.Yellow => "#FFD425",
        DuckColour.Black => "#17140F",
        DuckColour.White => "#FFFFFF",
        DuckColour.Mint => "#7FD8BE",
        DuckColour.Red => "#E5484D",
        DuckColour.Blue => "#6CB6FF",
        DuckColour.Lilac => "#B9A3F0",
        _ => "#7E9A4A",
    };

    public static string Price(decimal amount) => string.Create(CultureInfo.InvariantCulture, $"€{amount:0.00}");
}
