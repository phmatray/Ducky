#region search-api
using System.Collections.Immutable;

namespace DuckShop;

// Searching happens on the server, like the catalogue: it answers with the ids that match.
public interface ISearchApi
{
    Task<ImmutableList<string>> SearchAsync(string term, CancellationToken cancellationToken);
}

public sealed class FakeSearchApi(TimeProvider time) : ISearchApi
{
    // A short term matches more ducks, so the fake server takes longer to answer it:
    // "y" takes 600 ms, "ye" 450 ms, "yel" 300 ms. Typing fast leaves older, slower
    // searches still running when the newest one answers.
    public static TimeSpan Latency(string term) =>
        TimeSpan.FromMilliseconds(Math.Max(150, 750 - (150 * term.Length)));

    public async Task<ImmutableList<string>> SearchAsync(
        string term,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Latency(term), time, cancellationToken);
        return
        [
            .. FakeCatalogueApi.Ducks
                .Where(duck => duck.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(duck => duck.Id),
        ];
    }
}
#endregion
