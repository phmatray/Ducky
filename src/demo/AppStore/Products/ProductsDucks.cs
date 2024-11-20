// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Products;

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
            state => state.SelectImmutableArray(product => product.Category == "Electronics"),
            state => state.ById);

        _selectClothing = MemoizedSelector.Create<ProductState, ImmutableArray<Product>>(
            state => state.SelectImmutableArray(product => product.Category == "Clothing"),
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
    {
        return _selectElectronics(this);
    }

    public ImmutableArray<Product> SelectClothing()
    {
        return _selectClothing(this);
    }

    public decimal SelectTotalPriceOfElectronics()
    {
        return _selectTotalPriceOfElectronics(this);
    }

    public decimal SelectTotalPriceOfClothing()
    {
        return _selectTotalPriceOfClothing(this);
    }
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
        On<AddProduct>((state, action)
            => state.SetOne(action.Payload.Product));

        On<RemoveProduct>((state, action)
            => state.RemoveOne(action.Payload.ProductId));
    }

    public override ProductState GetInitialState()
    {
        return ProductState.Create([
            new Product(SampleIds.Id1, "iPhone 12", 799.99m, "Electronics"),
            new Product(SampleIds.Id2, "MacBook Pro", 1299.99m, "Electronics"),
            new Product(SampleIds.Id3, "Nike Air Max", 129, "Clothing"),
            new Product(SampleIds.Id4, "Adidas Original", 99, "Clothing"),
            new Product(SampleIds.Id5, "Samsung Galaxy S21", 699.99m, "Electronics"),
            new Product(SampleIds.Id6, "Bag Louis Vuitton", 1999.99m, "Clothing")
        ]);
    }
}

#endregion
