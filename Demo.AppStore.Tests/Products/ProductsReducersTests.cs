namespace Demo.AppStore.Tests.Products;

public class ProductsReducersTests
{
    private readonly ProductsReducers _sut = new();
    
    private readonly ProductState _initialState = ProductState.Create([
        new Product(SampleIds.Id1, "iPhone 12", 799.99m, "Electronics"),
        new Product(SampleIds.Id2, "MacBook Pro", 1299.99m, "Electronics"),
        new Product(SampleIds.Id3, "Nike Air Max", 129, "Clothing"),
        new Product(SampleIds.Id4, "Adidas Original", 99, "Clothing")
    ]);
    
    private const string Key = "products";
    
    [Fact]
    public void ProductsReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void ProductsReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }
    
    [Fact]
    public void ProductsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(ProductState));
    }
    
    [Fact]
    public void ProductsReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(2);
    }
    
    [Fact]
    public void AddProduct_ShouldAddNewProduct()
    {
        // Arrange
        var product = new Product(SampleIds.Id5, "AirPods Pro", 249.99m, "Electronics");
        var action = new AddProduct(product);

        // Act
        var state = _sut.Reduce(_initialState, action);

        // Assert
        state.SelectImmutableList().Should().HaveCount(5);
        state.SelectImmutableList().Should().Contain(product);
    }
    
    [Fact]
    public void RemoveProduct_ShouldRemoveProduct()
    {
        // Arrange
        var action = new RemoveProduct(SampleIds.Id1);

        // Act
        var state = _sut.Reduce(_initialState, action);

        // Assert
        state.SelectImmutableList().Should().HaveCount(3);
        state.SelectImmutableList().Should().NotContain(product => product.Id == SampleIds.Id1);
    }
    
    // Tests for Memoized Selectors
    
    [Fact]
    public void SelectElectronics_Should_Return_Electronics()
    {
        // Act
        var electronics = _sut.GetInitialState().SelectElectronics();

        // Assert
        electronics.Should().HaveCount(2);
        electronics.Should().OnlyContain(product => product.Category == "Electronics");
    }
    
    [Fact]
    public void SelectClothing_Should_Return_Clothing()
    {
        // Act
        var clothing = _sut.GetInitialState().SelectClothing();

        // Assert
        clothing.Should().HaveCount(2);
        clothing.Should().OnlyContain(product => product.Category == "Clothing");
    }
    
    [Fact]
    public void SelectTotalPriceOfElectronics_Should_Return_TotalPrice()
    {
        // Act
        var totalPrice = _sut.GetInitialState().SelectTotalPriceOfElectronics();

        // Assert
        totalPrice.Should().Be(2099.98m);
    }
    
    [Fact]
    public void SelectTotalPriceOfClothing_Should_Return_TotalPrice()
    {
        // Act
        var totalPrice = _sut.GetInitialState().SelectTotalPriceOfClothing();

        // Assert
        totalPrice.Should().Be(228m);
    }
}