using R3dux.Tests.TestModels;

namespace R3dux.Tests;

public class StoreTests
{
    private const string CounterKey = "counter";
    private readonly Store _sut = TestStoreFactory.CreateTestCounterStore();

    [Fact]
    public async Task Store_Should_Initialize_With_Provided_State()
    {
        // Act
        RootState result = await _sut.State.FirstAsync();

        // Assert
        result[CounterKey].Should().Be(10);
    }

    [Fact]
    public async Task Store_Should_Apply_IncrementAction()
    {
        // Act
        _sut.Dispatch(new Increment());
        RootState result = await _sut.State.FirstAsync();

        // Assert
        result[CounterKey].Should().Be(1);
    }

    [Fact]
    public async Task Store_Should_Apply_DecrementAction()
    {
        // Act
        _sut.Dispatch(new Decrement());
        RootState result = await _sut.State.Skip(1).FirstAsync();

        // Assert
        result[CounterKey].Should().Be(-1);
    }

    [Fact]
    public async Task Store_Should_Apply_SetValueAction()
    {
        // Arrange
        const int newValue = 5;

        // Act
        _sut.Dispatch(new SetValue(newValue));
        RootState result = await _sut.State.Skip(1).FirstAsync();

        // Assert
        result[CounterKey].Should().Be(newValue);
    }

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