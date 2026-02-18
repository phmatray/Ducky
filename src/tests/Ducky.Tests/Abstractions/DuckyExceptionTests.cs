namespace Ducky.Tests.Abstractions;

public class DuckyExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        const string message = "Test exception message";

        // Act
        var exception = new DuckyException(message);

        // Assert
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        const string message = "Outer exception message";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new DuckyException(message, innerException);

        // Assert
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBe(innerException);
    }

    [Fact]
    public void Constructor_Default_ShouldCreateValidException()
    {
        // Act
        var exception = new DuckyException();

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldNotBeEmpty(); // Default exception message
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void DuckyException_ShouldInheritFromException()
    {
        // Arrange
        var exception = new DuckyException("Test");

        // Assert
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void Constructor_WithNullMessage_ShouldHandleGracefully()
    {
        // Act
        var exception = new DuckyException(null!);

        // Assert
        exception.ShouldNotBeNull();
        // The base Exception class handles null messages
    }

    [Fact]
    public void Constructor_WithNullInnerException_ShouldHandleGracefully()
    {
        // Act
        var exception = new DuckyException("Test", null!);

        // Assert
        exception.Message.ShouldBe("Test");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void DuckyException_ShouldBeThrowable()
    {
        // Arrange
        const string message = "Test throw";

        // Act & Assert
        DuckyException exception = Should.Throw<DuckyException>(() => throw new DuckyException(message));
        exception.Message.ShouldBe(message);
    }

    [Fact]
    public void DuckyException_ShouldBeCatchableAsException()
    {
        // Arrange
        const string message = "Test catch";
        Exception? caughtException;

        // Act
        try
        {
            throw new DuckyException(message);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.ShouldNotBeNull();
        caughtException.ShouldBeOfType<DuckyException>();
        caughtException.Message.ShouldBe(message);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedExceptionInfo()
    {
        // Arrange
        var exception = new DuckyException("Test exception");

        // Act
        string result = exception.ToString();

        // Assert
        result.ShouldContain("DuckyException");
        result.ShouldContain("Test exception");
    }

    [Fact]
    public void StackTrace_ShouldBePopulated_WhenThrown()
    {
        // Arrange
        DuckyException? exception;

        // Act
        try
        {
            throw new DuckyException("Test");
        }
        catch (DuckyException ex)
        {
            exception = ex;
        }

        // Assert
        exception.ShouldNotBeNull();
        exception.StackTrace.ShouldNotBeNullOrEmpty();
    }
}