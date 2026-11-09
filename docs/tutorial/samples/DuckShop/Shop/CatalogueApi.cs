#region catalogue-api
using System.Collections.Immutable;

namespace DuckShop;

// Where the ducks come from. A real shop would call its server here with HttpClient.
public interface ICatalogueApi
{
    Task<ImmutableList<Duck>> GetDucksAsync(CancellationToken cancellationToken);
}

// A fake server that lives in the browser: it answers after a short delay, like a network.
// It waits on the TimeProvider it is given, so a test can move time forward instantly.
public sealed class FakeCatalogueApi(TimeProvider time) : ICatalogueApi
{
    public static readonly TimeSpan Latency = TimeSpan.FromMilliseconds(600);

    public static readonly ImmutableList<Duck> Ducks =
    [
        new("classic", "Classic Yellow", 4.50m),
        new("mellow", "Mellow Yellow", 5.20m),
        new("pirate", "Pirate Duck", 7.90m),
        new("disco", "Disco Duck", 9.50m),
        new("ninja", "Ninja Duck", 8.20m),
        new("rubber", "Rubber Ducky", 3.90m),
    ];

    public async Task<ImmutableList<Duck>> GetDucksAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Latency, time, cancellationToken);
        return Ducks;
    }
}
#endregion
