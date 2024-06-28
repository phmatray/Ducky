namespace R3dux.Tests;

public class StoreTests
{
    private readonly CounterReducer _reducer = new();

    [Fact]
    public async Task Store_Should_Initialize_With_Provided_State()
    {
        // Arrange
        const int initialState = 0;
        var store = new Store<int>(initialState, _reducer, []);

        // Act
        var result = await store.State.FirstAsync();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task Store_Should_Apply_IncrementAction()
    {
        // Arrange
        const int initialState = 0;
        var store = new Store<int>(initialState, _reducer, []);

        // Act
        var resultTask = store.State.Skip(1).FirstAsync();
        store.Dispatch(new Increment());
        var result = await resultTask;

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task Store_Should_Apply_DecrementAction()
    {
        // Arrange
        const int initialState = 0;
        var store = new Store<int>(initialState, _reducer, []);

        // Act
        var resultTask = store.State.Skip(1).FirstAsync();
        store.Dispatch(new Decrement());
        var result = await resultTask;

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public async Task Store_Should_Apply_SetValueAction()
    {
        // Arrange
        const int initialState = 0;
        var store = new Store<int>(initialState, _reducer, []);
        const int newValue = 5;

        // Act
        var resultTask = store.State.Skip(1).FirstAsync();
        store.Dispatch(new SetValue(newValue));
        var result = await resultTask;

        // Assert
        result.Should().Be(newValue);
    }

    [Fact]
    public async Task Store_Should_Notify_Subscribers_On_State_Change()
    {
        // Arrange
        const int initialState = 0;
        var store = new Store<int>(initialState, _reducer, []);
        var action = new Increment();
        var stateChanges = new List<int>();

        store.State.Subscribe(state => stateChanges.Add(state));

        // Act
        var resultTask = store.State.Skip(1).FirstAsync();
        store.Dispatch(action);
        await resultTask;

        // Assert
        stateChanges.Should().ContainInOrder(0, 1);
    }
}