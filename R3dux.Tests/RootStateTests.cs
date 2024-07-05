using R3dux.Exceptions;

namespace R3dux.Tests;

public class RootStateTests
{
    [Fact]
        public void AddOrUpdateSliceState_Should_Add_New_State()
        {
            // Arrange
            var rootState = new RootState();
            var key = "testKey";
            var initialState = new TestState { Value = 42 };

            // Act
            rootState.AddOrUpdateSliceState(key, initialState);

            // Assert
            rootState.ContainsKey(key).Should().BeTrue();
            rootState.Select<TestState>(key).Should().BeEquivalentTo(initialState);
        }

        [Fact]
        public void AddOrUpdateSliceState_Should_Update_Existing_State()
        {
            // Arrange
            var rootState = new RootState();
            var key = "testKey";
            var initialState = new TestState { Value = 42 };
            var updatedState = new TestState { Value = 99 };

            rootState.AddOrUpdateSliceState(key, initialState);

            // Act
            rootState.AddOrUpdateSliceState(key, updatedState);

            // Assert
            rootState.ContainsKey(key).Should().BeTrue();
            rootState.Select<TestState>(key).Should().BeEquivalentTo(updatedState);
        }

        [Fact]
        public void Indexer_Should_Get_And_Set_State()
        {
            // Arrange
            var rootState = new RootState();
            var key = "testKey";
            var initialState = new TestState { Value = 42 };

            // Act
            rootState[key] = initialState;
            var result = rootState[key];

            // Assert
            result.Should().BeEquivalentTo(initialState);
        }

        [Fact]
        public void Select_Should_Throw_Exception_If_State_Not_Found()
        {
            // Arrange
            var rootState = new RootState();
            var key = "nonExistingKey";

            // Act
            Action act = () => rootState.Select<TestState>(key);

            // Assert
            act.Should().Throw<R3duxException>()
                .WithMessage($"State with key '{key}' is not of type 'TestState'.");
        }

        [Fact]
        public void ContainsKey_Should_Return_True_If_Key_Exists()
        {
            // Arrange
            var rootState = new RootState();
            var key = "testKey";
            var initialState = new TestState { Value = 42 };

            rootState.AddOrUpdateSliceState(key, initialState);

            // Act
            var result = rootState.ContainsKey(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void ContainsKey_Should_Return_False_If_Key_Does_Not_Exist()
        {
            // Arrange
            var rootState = new RootState();
            var key = "nonExistingKey";

            // Act
            var result = rootState.ContainsKey(key);

            // Assert
            result.Should().BeFalse();
        }

        private class TestState
        {
            public int Value { get; set; }
        }
}