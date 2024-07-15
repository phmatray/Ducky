using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class StoreTests
{
    private readonly Store _sut = Factories.CreateTestCounterStore();

    [Fact]
    public void Store_Should_Initialize_With_Default_State()
    {
        // Act
        Observable<RootState> observable = _sut.RootStateObservable;
        RootState rootState = observable.FirstSync();
    
        // Assert
        rootState.Should().NotBeNull();
        rootState.Should().BeOfType<RootState>();
    }

    [Fact]
    public void Store_Should_Dispatch_StoreInitialized_Action_On_Initialization()
    {
        // Arrange
        IDispatcher dispatcher = _sut.Dispatcher;

        // Act
        dispatcher.ActionStream.Subscribe(action =>
        {
            // Assert
            action.Should().BeOfType<StoreInitialized>();
        });
            
        dispatcher.Dispatch(new StoreInitialized());
    }
    
    [Fact]
    public void Store_Should_Add_Slice_And_Propagate_State_Changes()
    {
        // Arrange
        var counterSlice = new TestCounterReducers();
        _sut.AddSlice(counterSlice);

        // Act
        counterSlice.OnDispatch(new TestIncrementAction());
        var updatedState = _sut.GetState<int>("test-counter");

        // Assert
        updatedState.Should().Be(11);
    }

    [Fact]
    public void Store_Should_Handle_Dispose_Correctly()
    {
        // Act
        _sut.Dispose();

        // Assert
        Action act = () => _sut.Dispatch(new TestIncrementAction());
        act.Should().Throw<ObjectDisposedException>();
    }
}