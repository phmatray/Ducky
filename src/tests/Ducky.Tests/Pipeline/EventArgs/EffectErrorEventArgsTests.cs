using Ducky.Pipeline;

namespace Ducky.Tests.Pipeline.EventArgs;

public class EffectErrorEventArgsTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        Type effectType = typeof(TestEffect);
        var action = new TestAction();

        // Act
        var eventArgs = new EffectErrorEventArgs(exception, effectType, action);

        // Assert
        eventArgs.Exception.ShouldBe(exception);
        eventArgs.EffectType.ShouldBe(effectType);
        eventArgs.Action.ShouldBe(action);
        eventArgs.IsHandled.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithIsHandledTrue_ShouldSetIsHandled()
    {
        // Arrange
        var exception = new Exception();
        Type effectType = typeof(TestEffect);
        var action = new TestAction();

        // Act
        var eventArgs = new EffectErrorEventArgs(exception, effectType, action, true);

        // Assert
        eventArgs.IsHandled.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullException_ShouldAcceptNull()
    {
        // Arrange
        Type effectType = typeof(TestEffect);
        var action = new TestAction();

        // Act
        var eventArgs = new EffectErrorEventArgs(null!, effectType, action);

        // Assert
        eventArgs.Exception.ShouldBeNull();
        eventArgs.EffectType.ShouldBe(effectType);
        eventArgs.Action.ShouldBe(action);
    }

    [Fact]
    public void Constructor_WithNullEffectType_ShouldAcceptNull()
    {
        // Arrange
        var exception = new Exception();
        var action = new TestAction();

        // Act
        var eventArgs = new EffectErrorEventArgs(exception, null!, action);

        // Assert
        eventArgs.Exception.ShouldBe(exception);
        eventArgs.EffectType.ShouldBeNull();
        eventArgs.Action.ShouldBe(action);
    }

    [Fact]
    public void Constructor_WithNullAction_ShouldAcceptNull()
    {
        // Arrange
        var exception = new Exception();
        Type effectType = typeof(TestEffect);

        // Act
        var eventArgs = new EffectErrorEventArgs(exception, effectType, null!);

        // Assert
        eventArgs.Exception.ShouldBe(exception);
        eventArgs.EffectType.ShouldBe(effectType);
        eventArgs.Action.ShouldBeNull();
    }

    [Fact]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        Type type = typeof(EffectErrorEventArgs);

        // Assert - Check that all properties only have getters
        type.GetProperty(nameof(EffectErrorEventArgs.Exception))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(EffectErrorEventArgs.EffectType))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(EffectErrorEventArgs.Action))!.CanWrite.ShouldBeFalse();
        type.GetProperty(nameof(EffectErrorEventArgs.IsHandled))!.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void ShouldInheritFromStoreEventArgs()
    {
        // Arrange
        var eventArgs = new EffectErrorEventArgs(new Exception(), typeof(TestEffect), new TestAction());

        // Assert
        eventArgs.ShouldBeAssignableTo<StoreEventArgs>();
    }

    [Fact]
    public void Constructor_WithComplexException_ShouldPreserveExceptionDetails()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner exception");
        var exception = new AggregateException("Outer exception", innerException);
        Type effectType = typeof(TestEffect);
        var action = new TestAction { Value = 99 };

        // Act
        var eventArgs = new EffectErrorEventArgs(exception, effectType, action);

        // Assert
        eventArgs.Exception.ShouldBe(exception);
        eventArgs.Exception.InnerException.ShouldBe(innerException);
        ((TestAction)eventArgs.Action).Value.ShouldBe(99);
    }

    [Fact]
    public void Constructor_WithDifferentEffectTypes_ShouldWorkCorrectly()
    {
        // Arrange
        (Type, object)[] typesAndActions =
        [
            (typeof(TestEffect), new TestAction()),
            (typeof(AnotherEffect), new AnotherAction()),
            (typeof(string), "test action")
        ];

        foreach ((Type effectType, object action) in typesAndActions)
        {
            // Act
            var eventArgs = new EffectErrorEventArgs(new Exception(), effectType, action);

            // Assert
            eventArgs.EffectType.ShouldBe(effectType);
            eventArgs.Action.ShouldBe(action);
        }
    }

    private record TestAction
    {
        public int Value { get; init; }
    }

    private record AnotherAction;

    private class TestEffect;

    private class AnotherEffect;
}