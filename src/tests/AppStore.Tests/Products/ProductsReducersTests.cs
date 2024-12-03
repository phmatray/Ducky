// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

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
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void ProductsReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void ProductsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<ProductState>();
    }

    [Fact]
    public void ProductsReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<ProductState, object, ProductState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(2);
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
        state.SelectEntities().Should().HaveCount(7);
        state.SelectEntities().Should().Contain(product);
    }

    [Fact]
    public void RemoveProduct_ShouldRemoveProduct()
    {
        // Arrange
        RemoveProduct action = new(SampleIds.Id1);

        // Act
        ProductState state = _sut.Reduce(_initialState, action);

        // Assert
        state.SelectEntities().Should().HaveCount(5);
        state.SelectEntities().Should().NotContain(product => product.Id == SampleIds.Id1);
    }

    [Fact]
    public void SelectElectronics_Should_Return_Electronics()
    {
        // Act
        ValueCollection<Product> electronics = _sut.GetInitialState().SelectElectronics();

        // Assert
        electronics.Should().HaveCount(3);
        electronics.Should().OnlyContain(product => product.Category == "Electronics");
    }

    [Fact]
    public void SelectClothing_Should_Return_Clothing()
    {
        // Act
        ValueCollection<Product> clothing = _sut.GetInitialState().SelectClothing();

        // Assert
        clothing.Should().HaveCount(3);
        clothing.Should().OnlyContain(product => product.Category == "Clothing");
    }

    [Fact]
    public void SelectTotalPriceOfElectronics_Should_Return_TotalPrice()
    {
        // Act
        decimal totalPrice = _sut.GetInitialState().SelectTotalPriceOfElectronics();

        // Assert
        totalPrice.Should().Be(2799.97m);
    }

    [Fact]
    public void SelectTotalPriceOfClothing_Should_Return_TotalPrice()
    {
        // Act
        decimal totalPrice = _sut.GetInitialState().SelectTotalPriceOfClothing();

        // Assert
        totalPrice.Should().Be(2227.99m);
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
