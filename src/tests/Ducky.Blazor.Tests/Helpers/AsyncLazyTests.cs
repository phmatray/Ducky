using Shouldly;
using Xunit;

namespace Ducky.Blazor.Tests.Helpers;

public class AsyncLazyTests
{
    [Fact]
    public async Task AsyncLazy_WithSyncFactory_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var callCount = 0;
        var asyncLazy = new AsyncLazy<int>(() =>
        {
            callCount++;
            return 42;
        });

        // Act
        var value1 = await asyncLazy.Value;
        var value2 = await asyncLazy.Value;
        var value3 = await asyncLazy.Value;

        // Assert
        value1.ShouldBe(42);
        value2.ShouldBe(42);
        value3.ShouldBe(42);
        callCount.ShouldBe(1);
    }

    [Fact]
    public async Task AsyncLazy_WithAsyncFactory_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var callCount = 0;
        var asyncLazy = new AsyncLazy<string>(async () =>
        {
            callCount++;
            await Task.Delay(10);
            return "test";
        });

        // Act
        var value1 = await asyncLazy.Value;
        var value2 = await asyncLazy.Value;
        var value3 = await asyncLazy.Value;

        // Assert
        value1.ShouldBe("test");
        value2.ShouldBe("test");
        value3.ShouldBe("test");
        callCount.ShouldBe(1);
    }

    [Fact]
    public async Task AsyncLazy_WithException_ShouldPropagateException()
    {
        // Arrange
        var asyncLazy = new AsyncLazy<int>(() => throw new InvalidOperationException("Test error"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await asyncLazy.Value);
    }

    [Fact]
    public async Task AsyncLazy_WithAsyncException_ShouldPropagateException()
    {
        // Arrange
        var asyncLazy = new AsyncLazy<int>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Async test error");
        });

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () => await asyncLazy.Value);
    }

    [Fact]
    public async Task AsyncLazy_ConcurrentAccess_ShouldInitializeOnlyOnce()
    {
        // Arrange
        var callCount = 0;
        var semaphore = new SemaphoreSlim(0);
        
        var asyncLazy = new AsyncLazy<int>(async () =>
        {
            Interlocked.Increment(ref callCount);
            await semaphore.WaitAsync();
            return 100;
        });

        // Act - Start multiple concurrent tasks
        var task1 = Task.Run(async () => await asyncLazy.Value);
        var task2 = Task.Run(async () => await asyncLazy.Value);
        var task3 = Task.Run(async () => await asyncLazy.Value);

        // Give tasks time to start
        await Task.Delay(50);
        
        // Release the semaphore to complete initialization
        semaphore.Release();

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert
        results.ShouldAllBe(v => v == 100);
        callCount.ShouldBe(1);
    }

    [Fact]
    public void AsyncLazy_IsValueCreated_ShouldWorkCorrectly()
    {
        // Arrange
        var asyncLazy = new AsyncLazy<string>(() => "test");

        // Act & Assert
        asyncLazy.IsValueCreated.ShouldBeFalse();
        
        var _ = asyncLazy.Value; // Access value
        
        asyncLazy.IsValueCreated.ShouldBeTrue();
    }

    [Fact]
    public async Task AsyncLazy_WithNullReturnValue_ShouldHandleCorrectly()
    {
        // Arrange
        var asyncLazy = new AsyncLazy<string?>(() => null);

        // Act
        var result = await asyncLazy.Value;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AsyncLazy_WithComplexObject_ShouldWorkCorrectly()
    {
        // Arrange
        var asyncLazy = new AsyncLazy<Dictionary<string, int>>(async () =>
        {
            await Task.Delay(1);
            return new Dictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2
            };
        });

        // Act
        var result = await asyncLazy.Value;

        // Assert
        result.ShouldNotBeNull();
        result["one"].ShouldBe(1);
        result["two"].ShouldBe(2);
    }
}