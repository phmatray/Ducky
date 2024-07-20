// using R3dux.Tests.TestModels;
//
// namespace R3dux.Tests;
//
// public class StoreWithEffectsTests
// {
//     [Fact]
//     public async Task IncrementEffect_Should_Set_New_Value_After_Delay()
//     {
//         // Arrange
//         const int initialState = 10;
//         IEffect[] effects = [new IncrementEffect()];
//         var store = TestStoreFactory.CreateTestCounterStore(initialState, effects);
//         var selectedValues = new List<int>();
//
//         store.State.Subscribe(state => selectedValues.Add(state));
//
//         // Act
//         store.Dispatch(new Increment());
//         await Task.Delay(2000); // Wait to allow the effect to complete
//
//         // Assert
//         selectedValues.Should().ContainInOrder(10, 11, 12); // Initial, increment, effect increment
//     }
// }