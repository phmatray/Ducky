namespace Ducky.Tests.Abstractions;

public class StateChangedEventArgsTests
{
    [Fact]
    public void Constructor_WithValidStates_ShouldInitializeProperties()
    {
        // Arrange
        const string sliceKey = "TestSlice";
        Type sliceType = typeof(TestState);
        var previousState = new TestState { Value = 10 };
        var newState = new TestState { Value = 20 };

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, newState, previousState);

        // Assert
        eventArgs.SliceKey.ShouldBe(sliceKey);
        eventArgs.SliceType.ShouldBe(sliceType);
        eventArgs.PreviousState.ShouldBe(previousState);
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void Constructor_WithNullPreviousState_ShouldBeAllowed()
    {
        // Arrange
        const string sliceKey = "TestSlice";
        Type sliceType = typeof(TestState);
        var newState = new TestState { Value = 20 };

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, newState);

        // Assert
        eventArgs.PreviousState.ShouldBeNull();
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void Constructor_WithNullNewState_ShouldAcceptNull()
    {
        // Arrange
        const string sliceKey = "TestSlice";
        Type sliceType = typeof(TestState);
        var previousState = new TestState();

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, null!, previousState);

        // Assert
        eventArgs.NewState.ShouldBeNull();
        eventArgs.PreviousState.ShouldBe(previousState);
    }

    [Fact]
    public void Constructor_WithNullSliceKey_ShouldAcceptNull()
    {
        // Arrange
        Type sliceType = typeof(TestState);
        var newState = new TestState();

        // Act
        var eventArgs = new StateChangedEventArgs(null!, sliceType, newState);

        // Assert
        eventArgs.SliceKey.ShouldBeNull();
        eventArgs.SliceType.ShouldBe(sliceType);
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void Constructor_WithEmptySliceKey_ShouldAcceptEmpty()
    {
        // Arrange
        Type sliceType = typeof(TestState);
        var newState = new TestState();

        // Act
        var eventArgs = new StateChangedEventArgs(string.Empty, sliceType, newState);

        // Assert
        eventArgs.SliceKey.ShouldBe(string.Empty);
        eventArgs.SliceType.ShouldBe(sliceType);
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void Constructor_WithNullSliceType_ShouldAcceptNull()
    {
        // Arrange
        const string sliceKey = "TestSlice";
        var newState = new TestState();

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, null!, newState);

        // Assert
        eventArgs.SliceKey.ShouldBe(sliceKey);
        eventArgs.SliceType.ShouldBeNull();
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void StateChangedEventArgs_ShouldInheritFromEventArgs()
    {
        // Arrange
        var eventArgs = new StateChangedEventArgs("TestSlice", typeof(TestState), new TestState());

        // Assert
        eventArgs.ShouldBeAssignableTo<EventArgs>();
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        Type type = typeof(StateChangedEventArgs);

        // Assert
        type.GetProperty(nameof(StateChangedEventArgs.SliceKey))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(StateChangedEventArgs.SliceType))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(StateChangedEventArgs.PreviousState))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(StateChangedEventArgs.NewState))!.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithDifferentStateTypes_ShouldWork()
    {
        // Arrange
        const string sliceKey = "AnonSlice";
        var previousState = new { Name = "Previous" };
        var newState = new { Name = "New" };
        Type sliceType = previousState.GetType();

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, newState, previousState);

        // Assert
        eventArgs.PreviousState.ShouldBe(previousState);
        eventArgs.NewState.ShouldBe(newState);
    }

    [Fact]
    public void Constructor_WithComplexStates_ShouldPreserveReferences()
    {
        // Arrange
        const string sliceKey = "ComplexSlice";
        Type sliceType = typeof(ComplexState);
        var list1 = new List<string> { "a", "b", "c" };
        var list2 = new List<string> { "x", "y", "z" };
        var previousState = new ComplexState { Items = list1 };
        var newState = new ComplexState { Items = list2 };

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, newState, previousState);

        // Assert
        eventArgs.PreviousState.ShouldBe(previousState);
        eventArgs.NewState.ShouldBe(newState);
        ReferenceEquals(((ComplexState?)eventArgs.PreviousState)?.Items, list1).ShouldBeTrue();
        ReferenceEquals(((ComplexState)eventArgs.NewState).Items, list2).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_FirstStateChange_PreviousStateShouldBeNull()
    {
        // This tests the typical scenario for initial state setup
        // Arrange
        const string sliceKey = "InitialSlice";
        Type sliceType = typeof(TestState);
        var initialState = new TestState { Value = 0 };

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, initialState);

        // Assert
        eventArgs.PreviousState.ShouldBeNull();
        eventArgs.NewState.ShouldBe(initialState);
    }

    [Fact]
    public void Constructor_WithValueTypes_ShouldBoxCorrectly()
    {
        // Arrange
        const string sliceKey = "ValueSlice";
        Type sliceType = typeof(int);
        const int previousState = 10;
        const int newState = 20;

        // Act
        var eventArgs = new StateChangedEventArgs(sliceKey, sliceType, newState, previousState);

        // Assert
        eventArgs.PreviousState.ShouldBe(10);
        eventArgs.NewState.ShouldBe(20);
        eventArgs.PreviousState.ShouldBeOfType<int>();
        eventArgs.NewState.ShouldBeOfType<int>();
    }

    private record TestState
    {
        public int Value { get; init; }
    }

    private class ComplexState
    {
        public List<string> Items { get; init; } = [];
    }
}