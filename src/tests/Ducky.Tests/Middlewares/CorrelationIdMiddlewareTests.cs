using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using Moq;

namespace Ducky.Tests.Middlewares;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        IDispatcher dispatcher = new Mock<IDispatcher>().Object;
        IStore store = new Mock<IStore>().Object;

        // Act
        await middleware.InitializeAsync(dispatcher, store);

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void AfterInitializeAllMiddlewares_ShouldCompleteSuccessfully()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);

        // Act
        middleware.AfterInitializeAllMiddlewares();

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void MayDispatchAction_ShouldAlwaysReturnTrue()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        // Act
        bool result = middleware.MayDispatchAction(action);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void BeforeDispatch_ShouldPublishCorrelationIdAssignedEvent()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        CorrelationIdAssignedEvent? publishedEvent = null;
        eventPublisher.Events
            .OfType<StoreEventArgs, CorrelationIdAssignedEvent>()
            .Subscribe(e => publishedEvent = e);

        // Act
        middleware.BeforeDispatch(action);

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal(action, publishedEvent.Action);
        Assert.NotEqual(Guid.Empty, publishedEvent.CorrelationId);
    }

    [Fact]
    public void BeforeDispatch_ShouldAssignNewCorrelationId()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        List<Guid> correlationIds = [];
        eventPublisher.Events
            .OfType<StoreEventArgs, CorrelationIdAssignedEvent>()
            .Subscribe(e => correlationIds.Add(e.CorrelationId));

        // Act - dispatch multiple times
        middleware.BeforeDispatch(action);
        middleware.BeforeDispatch(action);

        // Assert - each dispatch should get a unique correlation ID
        Assert.Equal(2, correlationIds.Count);
        Assert.NotEqual(correlationIds[0], correlationIds[1]);
        Assert.NotEqual(Guid.Empty, correlationIds[0]);
        Assert.NotEqual(Guid.Empty, correlationIds[1]);
    }

    [Fact]
    public void AfterDispatch_ShouldDoNothing()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        int eventCount = 0;
        eventPublisher.Events.Subscribe(_ => eventCount++);

        // Act
        middleware.AfterDispatch(action);

        // Assert - no events should be published
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void BeginInternalMiddlewareChange_ShouldReturnDisposable()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);

        // Act
        IDisposable result = middleware.BeginInternalMiddlewareChange();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IDisposable>(result);

        // Verify dispose doesn't throw
        result.Dispose();
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(null!));
    }
}
