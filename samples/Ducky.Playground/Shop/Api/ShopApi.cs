using System.Collections.Immutable;
using System.Globalization;
using Ducky.Playground.Shop.Catalogue;
using Ducky.Playground.Shop.Orders;

namespace Ducky.Playground.Shop.Api;

public sealed record Receipt(string OrderId, DateTimeOffset PlacedAt);

public sealed class PaymentDeclinedException : Exception
{
    public PaymentDeclinedException()
    {
    }

    public PaymentDeclinedException(string message)
        : base(message)
    {
    }

    public PaymentDeclinedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

// The shop's back end, faked in the browser: real latency on the injected TimeProvider, and every call honours its
// cancellation token, so a superseded search really stops waiting.
public sealed class ShopApi(TimeProvider time)
{
    private const string OrderPrefix = "RD-";

    // The warehouse's own stock, which a paid order reduces.
    private readonly Dictionary<string, int> _stock = Stockroom.Ducks.ToDictionary(static duck => duck.Id, static duck => duck.Stock, StringComparer.Ordinal);

    public async Task<ImmutableList<Duck>> GetCatalogueAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(650), time, cancellationToken);
        return [.. Stockroom.Ducks.Select(duck => duck with { Stock = _stock[duck.Id] })];
    }

    // Each term gets its own latency (200 to 740 ms), so typing fast leaves slower answers to older terms in flight.
    public async Task<ImmutableArray<string>> SearchAsync(string term, CancellationToken cancellationToken)
    {
        var latency = 200 + (90 * (term.Sum(static c => c) % 7));
        await Task.Delay(TimeSpan.FromMilliseconds(latency), time, cancellationToken);
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return [.. Stockroom.Ducks.Where(duck => words.All(word => Matches(duck, word))).Select(static duck => duck.Id)];
    }

    public async Task<Receipt> PayAsync(ImmutableList<OrderLine> lines, decimal total, bool rigged, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1600), time, cancellationToken);
        if (rigged)
        {
            throw new PaymentDeclinedException($"The card was declined for {DuckColours.Price(total)}. X-ray rigged this one; the next payment goes through.");
        }
        if (lines.Any(static line => line.DuckId == Stockroom.CursedId))
        {
            throw new PaymentDeclinedException("Our payment provider refuses to process the Cursed Duck. Take it out of the basket and pay again.");
        }

        foreach (var line in lines)
        {
            _stock[line.DuckId] = Math.Max(0, _stock[line.DuckId] - line.Quantity);
        }
        var placedAt = time.GetUtcNow();
        return new Receipt(OrderPrefix + placedAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), placedAt);
    }

    // The order id carries its placing time, so the status survives a reload: a step every three seconds after placing.
    public async Task<OrderStatus> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(250), time, cancellationToken);
        var placedAt = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(orderId.AsSpan(OrderPrefix.Length), CultureInfo.InvariantCulture));
        var steps = (int)((time.GetUtcNow() - placedAt).TotalSeconds / 3);
        return (OrderStatus)Math.Clamp(steps, 0, (int)OrderStatus.Delivered);
    }

    private static bool Matches(Duck duck, string word) =>
        duck.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
        || duck.Colour.ToString().Contains(word, StringComparison.OrdinalIgnoreCase)
        || duck.Blurb.Contains(word, StringComparison.OrdinalIgnoreCase)
        || duck.Tags.Any(tag => tag.Contains(word, StringComparison.OrdinalIgnoreCase));
}
