// namespace R3dux.Tests;
//
// public class SelectorsTests
// {
//     private readonly CounterReducers _reducer = new();
//
//     [Fact]
//     public async Task Select_Should_Return_Initial_Derived_State()
//     {
//         // Arrange
//         const int initialState = 10;
//         var store = CreateTestCounterStore(initialState);
//
//         // Act
//         var selectedValue = await store.GetState()
//             .Select(state => state * 2)
//             .FirstAsync();
//
//         // Assert
//         selectedValue.Should().Be(20);
//     }
//
//     [Fact]
//     public async Task Select_Should_React_To_State_Changes()
//     {
//         // Arrange
//         const int initialState = 10;
//         var store = CreateTestCounterStore(initialState);
//
//         // Act
//         var selectedValues = new List<int>();
//         store.GetState()
//             .Select(state => state * 2)
//             .Subscribe(value => selectedValues.Add(value));
//         
//         store.Dispatch(new Increment());
//         store.Dispatch(new Increment());
//         await Task.Delay(100); // Allow some time for async operations
//
//         // Assert
//         selectedValues.Should().ContainInOrder(20, 22, 24);
//     }
// }