// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Products;

public sealed class ProductsReducersTests : IDisposable
{
    private const string Key = "products";

    private readonly ProductsReducers _sut = new();

    private readonly ProductState _initialState = ProductState.Create([
        new Product(SampleIds.Id1, "iPhone 12", 799.99m, "Electronics"),
        new Product(SampleIds.Id2, "MacBook Pro", 1299.99m, "Electronics"),
        new Product(SampleIds.Id3, "Nike Air Max", 129, "Clothing"),
        new Product(SampleIds.Id4, "Adidas Original", 99, "Clothing"),
        new Product(SampleIds.Id5, "Samsung Galaxy S21", 699.99m, "Electronics"),
        new Product(SampleIds.Id6, "Bag Louis Vuitton", 1999.99m, "Clothing")
    ]);

    private bool _disposed;

    [Fact]
    public void ProductsReducers_Should_Return_Initial_State()
    {
        // Act
        ProductState initialState = _sut.GetInitialState();

        // Assert
        initialState.SelectEntities().Count.ShouldBe(6);
        initialState.SelectEntities().ShouldBeEquivalentTo(_initialState.SelectEntities());
    }

    [Fact]
    public void ProductsReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.ShouldBe(Key);
    }

    [Fact]
    public void ProductsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.FullName.ShouldBe(typeof(ProductState).FullName);
    }

    [Fact]
    public void ProductsReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<ProductState, object, ProductState>> reducers = _sut.Reducers;

        // Assert
        reducers.Count.ShouldBe(2);
    }

    [Fact]
    public void AddProduct_ShouldAddNewProduct()
    {
        // Arrange
        Product product = new(SampleIds.Id7, "AirPods Pro", 249.99m, "Electronics");
        AddProduct action = new(product);

        // Act
        ProductState state = _sut.Reduce(_initialState, action);

        // Assert
        state.SelectEntities().Count.ShouldBe(7);
        state.SelectEntities().ShouldContain(product);
    }

    [Fact]
    public void RemoveProduct_ShouldRemoveProduct()
    {
        // Arrange
        RemoveProduct action = new(SampleIds.Id1);

        // Act
        ProductState state = _sut.Reduce(_initialState, action);

        // Assert
        state.SelectEntities().Count.ShouldBe(5);
        state.SelectEntities().ShouldNotContain(product => product.Id == SampleIds.Id1);
    }

    [Fact]
    public void SelectElectronics_Should_Return_Electronics()
    {
        // Act
        ValueCollection<Product> electronics = _sut.GetInitialState().SelectElectronics();

        // Assert
        electronics.Count.ShouldBe(3);
        foreach (Product product in electronics)
        {
            product.Category.ShouldBe("Electronics");
        }
    }

    [Fact]
    public void SelectClothing_Should_Return_Clothing()
    {
        // Act
        ValueCollection<Product> clothing = _sut.GetInitialState().SelectClothing();

        // Assert
        clothing.Count.ShouldBe(3);
        foreach (Product product in clothing)
        {
            product.Category.ShouldBe("Clothing");
        }
    }

    [Fact]
    public void SelectTotalPriceOfElectronics_Should_Return_TotalPrice()
    {
        // Act
        decimal totalPrice = _sut.GetInitialState().SelectTotalPriceOfElectronics();

        // Assert
        totalPrice.ShouldBe(2799.97m);
    }

    [Fact]
    public void SelectTotalPriceOfClothing_Should_Return_TotalPrice()
    {
        // Act
        decimal totalPrice = _sut.GetInitialState().SelectTotalPriceOfClothing();

        // Assert
        totalPrice.ShouldBe(2227.99m);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
