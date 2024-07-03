// namespace R3dux.Tests;
//
// public class IncrementEffectTests
// {
//     [Fact]
//     public async Task IncrementEffect_Should_Dispatch_SetValueAction()
//     {
//         // Arrange
//         const int initialState = 10;
//         var effect = new IncrementEffect();
//         var actions = new Subject<object>();
//         var dispatchedActions = new List<object>();
//         var state = Observable.Return(initialState);
//
//         effect
//             .Handle(actions, state)
//             .Subscribe(dispatchedActions.Add);
//
//         // Act
//         actions.OnNext(new Increment());
//         await Task.Delay(1000); // Wait to allow the effect to complete
//
//         // Assert
//         dispatchedActions.Should().HaveCount(1);
//         dispatchedActions.First().Should().BeOfType<SetValue>();
//         ((SetValue)dispatchedActions.First()).Value.Should().Be(11);
//     }
// }