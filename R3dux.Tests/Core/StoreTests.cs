using R3dux.Tests.TestModels;

namespace R3dux.Tests.Core;

public class StoreTests
{
    private const string CounterKey = "counter";
    private readonly Store _sut = TestStoreFactory.CreateTestCounterStore();

    [Fact]
    public void Store_Should_Initialize_With_Default_State()
    {
        // Act
        var rootState = _sut.GetRootState();

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
        var counterSlice = new CounterSlice();
        _sut.AddSlice(counterSlice);

        // Act
        counterSlice.OnDispatch(new Increment());
        var updatedState = _sut.GetState<int>("counter");

        // Assert
        updatedState.Should().Be(11);
    }

    [Fact]
    public void Store_Should_Handle_Dispose_Correctly()
    {
        // Act
        _sut.Dispose();

        // Assert
        Action act = () => _sut.Dispatch(new Increment());
        act.Should().Throw<ObjectDisposedException>();
    }

    // [Fact]
    // public void Store_Should_Add_Effects_And_Handle_Actions()
    // {
    //     // Arrange
    //     var actionsSubject = new Subject<IAction>();
    //     _dispatcherMock.Setup(d => d.ActionStream).Returns(actionsSubject.AsObservable());
    //
    //     var effectMock = new Mock<IEffect>();
    //     effectMock.Setup(e => e.Handle(It.IsAny<IObservable<IAction>>(), It.IsAny<Store>()))
    //         .Returns(actionsSubject.Where(a => a is Increment)
    //             .Select(_ => new Reset())
    //             .AsObservable());
    //
    //     _store.AddEffect(effectMock.Object);
    //
    //     // Act
    //     _store.Dispatch(new Increment());
    //
    //     // Assert
    //     _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<Reset>()), Times.Once);
    // }
    
    
    //
    //
    // [Fact]
    // public async Task Store_Should_Initialize_With_Provided_State()
    // {
    //     // Act
    //     RootState result = await _sut.RootState.FirstAsync();
    //
    //     // Assert
    //     result[CounterKey].Should().Be(10);
    // }
    //
    // [Fact]
    // public async Task Store_Should_Apply_IncrementAction()
    // {
    //     // Act
    //     _sut.Dispatch(new Increment());
    //     RootState result = await _sut.RootState.FirstAsync();
    //
    //     // Assert
    //     result[CounterKey].Should().Be(1);
    // }
    //
    // [Fact]
    // public async Task Store_Should_Apply_DecrementAction()
    // {
    //     // Act
    //     _sut.Dispatch(new Decrement());
    //     RootState result = await _sut.RootState.Skip(1).FirstAsync();
    //
    //     // Assert
    //     result[CounterKey].Should().Be(-1);
    // }
    //
    // [Fact]
    // public async Task Store_Should_Apply_SetValueAction()
    // {
    //     // Arrange
    //     const int newValue = 5;
    //
    //     // Act
    //     _sut.Dispatch(new SetValue(newValue));
    //     RootState result = await _sut.RootState.Skip(1).FirstAsync();
    //
    //     // Assert
    //     result[CounterKey].Should().Be(newValue);
    // }

    // [Fact]
    // public async Task Store_Should_Notify_Subscribers_On_State_Change()
    // {
    //     // Arrange
    //     var action = new Increment();
    //     var stateChanges = new List<int>();
    //
    //     _sut.State.Subscribe(state => stateChanges.Add(state));
    //
    //     // Act
    //     _sut.Dispatch(action);
    //     await _sut.State.Skip(1).FirstAsync();
    //
    //     // Assert
    //     stateChanges.Should().ContainInOrder(0, 1);
    // }
}