namespace R3dux.Tests;

public class StoreWithEffectsTests
{
    private readonly CounterReducer _reducer = new();

    [Fact]
    public async Task IncrementEffect_Should_Set_New_Value_After_Delay()
    {
        // Arrange
        const int initialState = 10;
        var effects = new List<Effect<int>> { new IncrementEffect() };
        var store = new Store<int>(initialState, _reducer, effects);
        var selectedValues = new List<int>();

        store.State.Subscribe(state => selectedValues.Add(state));

        // Act
        store.Dispatch(new Increment());
        await Task.Delay(2000); // Wait to allow the effect to complete

        // Assert
        selectedValues.Should().ContainInOrder(10, 11, 12); // Initial, increment, effect increment
    }
}