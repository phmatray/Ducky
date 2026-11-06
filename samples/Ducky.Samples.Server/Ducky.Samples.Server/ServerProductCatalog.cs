using Ducky.Samples.Server.Client;

namespace Ducky.Samples.Server;

/// <summary>The server catalogue; it counts every load (prerender and <c>/api/products</c>) for the no-double-load spec.</summary>
public sealed class ServerProductCatalog : IProductCatalog
{
    private static readonly IReadOnlyList<string> _products = ["Rubber duck", "Duck pond", "Bread crumbs"];
    private int _loads;

    /// <summary>How many times products were loaded since the app started.</summary>
    public int Loads => Volatile.Read(ref _loads);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _loads);
        return Task.FromResult(_products);
    }
}
