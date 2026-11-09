// A duck is an entity of the catalogue's EntityState. An adjustment that would take its
// stock below zero throws in the reducer, so the store reports ReducerFailed.
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

// IEntity<string> gives the duck the id that EntityState indexes it by.
public sealed record Duck(
    string Id,
    string Name,
    DuckColour Colour,
    decimal Price,
    int Stock,
    ImmutableArray<string> Tags,
    string Blurb) : IEntity<string>
{
    public bool InStock => Stock > 0;

    public Duck Restocked(int delta) => Stock + delta < 0
        ? throw new InvalidOperationException(
            $"{Name} can't go below zero stock ({Stock} {delta:+#;-#;0}).")
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
}

public static class Money
{
    public static string Format(decimal amount) =>
        string.Create(CultureInfo.InvariantCulture, $"€{amount:0.00}");
}
