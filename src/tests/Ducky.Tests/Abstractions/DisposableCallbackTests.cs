namespace Ducky.Tests.Abstractions;

public class DisposableCallbackTests
{
    [Fact]
    public void Constructor_WithNullCallback_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DisposableCallback(null!))
            .ParamName
            .ShouldBe("onDispose");
    }

    [Fact]
    public void Constructor_WithValidCallback_ShouldNotThrow()
    {
        // Act
        var disposable = new DisposableCallback(() => { });

        // Assert
        disposable.ShouldNotBeNull();
    }

    [Fact]
    public void Dispose_ShouldExecuteCallbackOnce()
    {
        // Arrange
        int callCount = 0;
        var disposable = new DisposableCallback(() => callCount++);

        // Act
        disposable.Dispose();

        // Assert
        callCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldExecuteCallbackOnlyOnce()
    {
        // Arrange
        int callCount = 0;
        var disposable = new DisposableCallback(() => callCount++);

        // Act
        disposable.Dispose();
        disposable.Dispose();
        disposable.Dispose();

        // Assert
        callCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_ShouldBeThreadSafe()
    {
        // Arrange
        int callCount = 0;
        var disposable = new DisposableCallback(() =>
        {
            Thread.Sleep(10); // Simulate some work
            Interlocked.Increment(ref callCount);
        });

        // Act - Dispose from multiple threads
        Task[] tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => disposable.Dispose()))
            .ToArray();

#pragma warning disable xUnit1031
        Task.WaitAll(tasks, TestContext.Current.CancellationToken);
#pragma warning restore xUnit1031

        // Assert
        callCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_WithExceptionInCallback_ShouldPropagateException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        var disposable = new DisposableCallback(() => throw expectedException);

        // Act & Assert
        InvalidOperationException actualException = Should.Throw<InvalidOperationException>(() => disposable.Dispose());
        actualException.ShouldBe(expectedException);
    }

    [Fact]
    public void Dispose_WithExceptionInCallback_ShouldStillMarkAsDisposed()
    {
        // Arrange
        int callCount = 0;
        var disposable = new DisposableCallback(() =>
        {
            callCount++;
            throw new Exception("Test");
        });

        // Act
        try
        {
            disposable.Dispose();
        }
        catch
        {
            // Ignore the exception
        }

        // Try to dispose again
        Should.NotThrow(() => disposable.Dispose());

        // Assert - Should still only be called once
        callCount.ShouldBe(1);
    }

    [Fact]
    public void Dispose_WithComplexCallback_ShouldExecuteCorrectly()
    {
        // Arrange
        var items = new List<string>();
        var disposable = new DisposableCallback(() =>
        {
            items.Add("First");
            items.Add("Second");
            items.Add("Third");
        });

        // Act
        disposable.Dispose();

        // Assert
        items.ShouldBe(new[] { "First", "Second", "Third" });
    }

    [Fact]
    public void DisposableCallback_ShouldImplementIDisposable()
    {
        // Arrange
        var disposable = new DisposableCallback(() => { });

        // Assert
        disposable.ShouldBeAssignableTo<IDisposable>();
    }

    [Fact]
    public void Dispose_InUsingStatement_ShouldWork()
    {
        // Arrange
        var disposed = false;

        // Act
        using (new DisposableCallback(() => disposed = true))
        {
            disposed.ShouldBeFalse();
        }

        // Assert
        disposed.ShouldBeTrue();
    }
}