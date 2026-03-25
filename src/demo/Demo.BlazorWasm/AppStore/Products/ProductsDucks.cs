// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

#region State

public record ProductState : NormalizedState<Guid, Product, ProductState>
{
    private readonly Func<ProductState, ImmutableArray<Product>> _selectElectronics;
    private readonly Func<ProductState, ImmutableArray<Product>> _selectClothing;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfElectronics;
    private readonly Func<ProductState, decimal> _selectTotalPriceOfClothing;

    public ProductState()
    {
        _selectElectronics = MemoizedSelector.Create<ProductState, ImmutableArray<Product>>(
            state => state.SelectEntities(product => product.Category == "Electronics"),
            state => state.ById);

        _selectClothing = MemoizedSelector.Create<ProductState, ImmutableArray<Product>>(
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
    public ImmutableArray<Product> SelectElectronics()
        => _selectElectronics(this);

    public ImmutableArray<Product> SelectClothing()
        => _selectClothing(this);

    public decimal SelectTotalPriceOfElectronics()
        => _selectTotalPriceOfElectronics(this);

    public decimal SelectTotalPriceOfClothing()
        => _selectTotalPriceOfClothing(this);
}

#endregion

#region Actions

[DuckyAction]
public sealed record AddProduct(Product Product);

[DuckyAction]
public sealed record RemoveProduct(Guid ProductId);

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
        => state.SetOne(action.Product);

    private static ProductState Reduce(ProductState state, RemoveProduct action)
        => state.RemoveOne(action.ProductId);
}

#endregion
