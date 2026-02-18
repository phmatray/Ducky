using Ducky.Pipeline;

namespace Ducky.Tests.Pipeline.EventArgs;

public class ActionAbortedEventArgsTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var action = new TestAction { Value = 42 };
        var context = new ActionContext(action);
        const string reason = "Test abort reason";

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        eventArgs.Context.ShouldBe(context);
        eventArgs.Reason.ShouldBe(reason);
    }

    [Fact]
    public void Constructor_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        const string reason = "Test reason";

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ActionAbortedEventArgs(null!, reason))
            .ParamName
            .ShouldBe("context");
    }

    [Fact]
    public void Constructor_WithNullReason_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new ActionContext(new TestAction());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new ActionAbortedEventArgs(context, null!))
            .ParamName
            .ShouldBe("reason");
    }

    [Fact]
    public void Constructor_WithEmptyReason_ShouldBeAllowed()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        string reason = string.Empty;

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        eventArgs.Reason.ShouldBe(string.Empty);
    }

    [Fact]
    public void ShouldInheritFromStoreEventArgs()
    {
        // Arrange
        var eventArgs = new ActionAbortedEventArgs(new ActionContext(new TestAction()), "Reason");

        // Assert
        eventArgs.ShouldBeAssignableTo<StoreEventArgs>();
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        Type type = typeof(ActionAbortedEventArgs);

        // Assert
        type.GetProperty(nameof(ActionAbortedEventArgs.Context))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(ActionAbortedEventArgs.Reason))!.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithAbortedContext_ShouldWorkCorrectly()
    {
        // Arrange
        var action = new TestAction { Value = 99 };
        var context = new ActionContext(action);
        context.Abort(); // Mark context as aborted
        const string reason = "Action was cancelled";

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        eventArgs.Context.IsAborted.ShouldBeTrue();
        eventArgs.Context.Action.ShouldBe(action);
    }

    [Fact]
    public void Constructor_WithContextContainingMetadata_ShouldPreserveContext()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        context.SetMetadata("key1", "value1");
        context.SetMetadata("key2", 42);
        const string reason = "Metadata test";

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        eventArgs.Context.Metadata.Count.ShouldBe(2);
        eventArgs.Context.TryGetMetadata("key1", out string? value1).ShouldBeTrue();
        value1.ShouldBe("value1");
        eventArgs.Context.TryGetMetadata("key2", out int value2).ShouldBeTrue();
        value2.ShouldBe(42);
    }

    [Fact]
    public void Constructor_WithLongReason_ShouldAccept()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        var longReason = new string('a', 1000); // Very long reason

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, longReason);

        // Assert
        eventArgs.Reason.ShouldBe(longReason);
        eventArgs.Reason.Length.ShouldBe(1000);
    }

    [Fact]
    public void Constructor_WithWhitespaceReason_ShouldBeAllowed()
    {
        // Arrange
        var context = new ActionContext(new TestAction());
        const string reason = "   ";

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        eventArgs.Reason.ShouldBe("   ");
    }

    [Fact]
    public void Constructor_PreservesActionReference()
    {
        // Arrange
        var action = new TestAction { Value = 123 };
        var context = new ActionContext(action);
        const string reason = "Test";

        // Act
        var eventArgs = new ActionAbortedEventArgs(context, reason);

        // Assert
        ReferenceEquals(eventArgs.Context.Action, action).ShouldBeTrue();
        ((TestAction)eventArgs.Context.Action).Value.ShouldBe(123);
    }

    private record TestAction
    {
        public int Value { get; init; }
    }
}