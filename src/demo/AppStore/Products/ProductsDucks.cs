// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Products;

#region State

public record ProductState : NormalizedState<Guid, Product, ProductState>
{
    private readonly Func<ProductState, ValueCollection<Product>> _selectElectronics;
    private readonly Func<ProductState, ValueCollection<Product>> _selectClothing;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfElectronics;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfClothing;

    public ProductState()
    {
        _selectElectronics = MemoizedSelector.Create<ProductState, ValueCollection<Product>>(
            state => state.SelectEntities(product => product.Category == "Electronics"),
            state => state.ById);

        _selectClothing = MemoizedSelector.Create<ProductState, ValueCollection<Product>>(
            state => state.SelectEntities(product => product.Category == "Clothing"),
            state => state.ById);

        _selectTotalPriceOfElectronics = MemoizedSelector.Compose(
            _selectElectronics,
            products => products.Sum(product => product.Price),
            state => state.ById);

        _selectTotalPriceOfClothing = MemoizedSelector.Compose(
            _selectClothing,
            products => products.Sum(product => product.Price),
            state => state.ById);
    }

    // Memoized Selectors
    public ValueCollection<Product> SelectElectronics()
        => _selectElectronics(this);

    public ValueCollection<Product> SelectClothing()
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
    public AddProduct(Product product)
        : base(new ActionPayload(product), ActionMeta.Create())
    {
    }

    public override string TypeKey => "products/add";

    public sealed record ActionPayload(Product Product);
}

public sealed record RemoveProduct
    : Fsa<RemoveProduct.ActionPayload, ActionMeta>
{
    public RemoveProduct(in Guid productId)
        : base(new ActionPayload(productId), ActionMeta.Create())
    {
    }

    public override string TypeKey => "products/remove";

    public sealed record ActionPayload(Guid ProductId);
}

#endregion

#region Reducers

public record ProductsReducers : SliceReducers<ProductState>
{
    public ProductsReducers()
    {
        On<AddProduct>(Reduce);
        On<RemoveProduct>(Reduce);
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

    private static ProductState Reduce(ProductState state, AddProduct action)
        => state.SetOne(action.Payload.Product);

    private static ProductState Reduce(ProductState state, RemoveProduct action)
        => state.RemoveOne(action.Payload.ProductId);
}

#endregion
