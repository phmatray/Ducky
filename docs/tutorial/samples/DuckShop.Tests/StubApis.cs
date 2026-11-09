#region test-stub-apis
using System.Collections.Immutable;

namespace DuckShop.Tests;

// Test doubles for the catalogue server: one answers at once, one fails. The other tests use
// the app's own fakes, FakeCatalogueApi and FakeSearchApi, on the test store's fake clock.
internal sealed class InstantCatalogueApi : ICatalogueApi
{
    public async Task<ImmutableList<Duck>> GetDucksAsync(CancellationToken cancellationToken)
    {
        await Task.Yield(); // a real server answers later, never inline
        return FakeCatalogueApi.Ducks;
    }
}

internal sealed class OfflineCatalogueApi : ICatalogueApi
{
    public Task<ImmutableList<Duck>> GetDucksAsync(CancellationToken cancellationToken) =>
        Task.FromException<ImmutableList<Duck>>(
            new HttpRequestException("The warehouse is offline."));
}
#endregion
