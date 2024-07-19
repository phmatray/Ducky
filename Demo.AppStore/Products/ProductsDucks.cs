namespace Demo.AppStore;

#region State

public record ProductState : NormalizedState<Guid, Product, ProductState>
{
    private readonly Func<ProductState, ImmutableList<Product>> _selectElectronics;
    private readonly Func<ProductState, ImmutableList<Product>> _selectClothing;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfElectronics;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfClothing;

    public ProductState()
    {
        _selectElectronics = MemoizedSelector.Create<ProductState, ImmutableList<Product>>(
            state => state.SelectImmutableList(product => product.Category == "Electronics"),
            state => state.ById
        );

        _selectClothing = MemoizedSelector.Create<ProductState, ImmutableList<Product>>(
            state => state.SelectImmutableList(product => product.Category == "Clothing"),
            state => state.ById
        );

        _selectTotalPriceOfElectronics = MemoizedSelector.Compose(
            _selectElectronics,
            products => products.Sum(product => product.Price),
            state => state.ById
        );

        _selectTotalPriceOfClothing = MemoizedSelector.Compose(
            _selectClothing,
            products => products.Sum(product => product.Price),
            state => state.ById
        );
    }

    // Memoized Selectors
    public ImmutableList<Product> SelectElectronics()
        => _selectElectronics(this);

    public ImmutableList<Product> SelectClothing()
        => _selectClothing(this);

    public decimal SelectTotalPriceOfElectronics()
        => _selectTotalPriceOfElectronics(this);

    public decimal SelectTotalPriceOfClothing()
        => _selectTotalPriceOfClothing(this);
}

#endregion

#region Actions

public sealed record AddProduct
    : Fsa<AddProduct.ActionPayload, ActionMeta>
{
    public sealed record ActionPayload(Product Product);

    public override string TypeKey => "products/add";

    public AddProduct(Product product)
        : base(new ActionPayload(product), ActionMeta.Create()) { }
}

public sealed record RemoveProduct
    : Fsa<RemoveProduct.ActionPayload, ActionMeta>
{
    public sealed record ActionPayload(Guid ProductId);

    public override string TypeKey => "products/remove";

    public RemoveProduct(Guid productId)
        : base(new ActionPayload(productId), ActionMeta.Create()) { }
}

#endregion

#region Reducers

public record ProductsReducers : SliceReducers<ProductState>
{
    public ProductsReducers()
    {
        Map<AddProduct>((state, action)
            => state.SetOne(action.Payload.Product));
        
        Map<RemoveProduct>((state, action)
            => state.RemoveOne(action.Payload.ProductId));
    }

    public override ProductState GetInitialState()
        => ProductState.Create([
            new Product(SampleIds.Id1, "iPhone 12", 799.99m, "Electronics"),
            new Product(SampleIds.Id2, "MacBook Pro", 1299.99m, "Electronics"),
            new Product(SampleIds.Id3, "Nike Air Max", 129, "Clothing"),
            new Product(SampleIds.Id4, "Adidas Original", 99, "Clothing"),
            new Product(SampleIds.Id5, "Samsung Galaxy S21", 699.99m, "Electronics"),
            new Product(SampleIds.Id6, "Bag Louis Vuitton", 1999.99m, "Clothing")
        ]);
}

#endregion
