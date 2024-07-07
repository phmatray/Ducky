using R3dux.Tests.TestModels;

namespace R3dux.Tests;

public class StoreTests
{
    private readonly Store _sut = TestStoreFactory.CreateTestCounterStore();

    [Fact]
    public async Task Store_Should_Initialize_With_Provided_State()
    {
        // Act
        var result = await _sut.State.FirstAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task Store_Should_Apply_IncrementAction()
    {
        // Act
        var resultTask = _sut.State.Skip(1).FirstAsync();
        _sut.Dispatch(new Increment());
        var result = await resultTask;

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task Store_Should_Apply_DecrementAction()
    {
        // Act
        var resultTask = _sut.State.Skip(1).FirstAsync();
        _sut.Dispatch(new Decrement());
        var result = await resultTask;

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public async Task Store_Should_Apply_SetValueAction()
    {
        // Arrange
        const int newValue = 5;

        // Act
        var resultTask = _sut.State.Skip(1).FirstAsync();
        _sut.Dispatch(new SetValue(newValue));
        var result = await resultTask;

        // Assert
        result.Should().Be(newValue);
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
    //     var resultTask = _sut.State.Skip(1).FirstAsync();
    //     _sut.Dispatch(action);
    //     await resultTask;
    //
    //     // Assert
    //     stateChanges.Should().ContainInOrder(0, 1);
    // }
}