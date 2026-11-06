using System.Net.Http.Json;

namespace Ducky.Samples.Server.Client;

/// <summary>The product list: a Prerender slice loaded by an effect, so it is loaded once per page (prerender or not).</summary>
public sealed record ProductsState(IReadOnlyList<string>? Items);

/// <summary>The catalogue answered.</summary>
[ActionType("products/loaded")]
public sealed record ProductsLoaded(IReadOnlyList<string> Items);

/// <summary>The products slice.</summary>
[Prerender]
public sealed class ProductsSlice : Slice<ProductsState>
{
    /// <summary>Registers the reducers.</summary>
    public ProductsSlice() => On<ProductsLoaded>((_, action) => new ProductsState(action.Items));

    /// <inheritdoc />
    public override string Key => "products";

    /// <inheritdoc />
    protected override ProductsState Initial => new(Items: null);
}

/// <summary>Where products come from: in memory on the server, over HTTP in the browser.</summary>
public interface IProductCatalog
{
    /// <summary>Loads the products.</summary>
    Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken);
}

/// <summary>The browser catalogue: the server's <c>/api/products</c>.</summary>
public sealed class HttpProductCatalog(HttpClient http) : IProductCatalog
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAsync(CancellationToken cancellationToken) =>
        await http.GetFromJsonAsync(new Uri("api/products", UriKind.Relative), SampleJson.Default.StringArray, cancellationToken).ConfigureAwait(false) ?? [];
}

/// <summary>The one load-effect rule (SPEC §11.3): skip the load when storage or the prerender seed restored the slice.</summary>
public sealed class LoadProducts(IProductCatalog catalog) : Effect<StoreInitialized>
{
    /// <inheritdoc />
    public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<ProductsState>())
        {
            context.Dispatch(new ProductsLoaded(await catalog.GetAsync(cancellationToken).ConfigureAwait(false)));
        }
    }
}
