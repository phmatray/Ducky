using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;

namespace Ducky.Tests.Middlewares;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InitializeAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        IDispatcher dispatcher = A.Fake<IDispatcher>();
        IStore store = A.Fake<IStore>();

        // Act
        await middleware.InitializeAsync(dispatcher, store);

        // Assert - just verify no exception is thrown
        true.ShouldBeTrue();
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
        true.ShouldBeTrue();
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
        result.ShouldBeTrue();
    }

    [Fact]
    public void BeforeReduce_ShouldPublishCorrelationIdAssignedEvent()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        CorrelationIdAssignedEvent? publishedEvent = null;
        eventPublisher.EventPublished += (_, args) =>
        {
            if (args is not CorrelationIdAssignedEvent evt)
            {
                return;
            }

            publishedEvent = evt;
        };

        // Act
        middleware.BeforeReduce(action);

        // Assert
        publishedEvent.ShouldNotBeNull();
        publishedEvent.Action.ShouldBe(action);
        publishedEvent.CorrelationId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void BeforeReduce_ShouldAssignNewCorrelationId()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        List<Guid> correlationIds = [];
        eventPublisher.EventPublished += (_, args) =>
        {
            if (args is not CorrelationIdAssignedEvent evt)
            {
                return;
            }

            correlationIds.Add(evt.CorrelationId);
        };

        // Act - dispatch multiple times
        middleware.BeforeReduce(action);
        middleware.BeforeReduce(action);

        // Assert - each dispatch should get a unique correlation ID
        correlationIds.Count.ShouldBe(2);
        correlationIds[0].ShouldNotBe(correlationIds[1]);
        correlationIds[0].ShouldNotBe(Guid.Empty);
        correlationIds[1].ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void AfterReduce_ShouldDoNothing()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();

        int eventCount = 0;
        eventPublisher.EventPublished += (_, _) => eventCount++;

        // Act
        middleware.AfterReduce(action);

        // Assert - no events should be published
        eventCount.ShouldBe(0);
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
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<IDisposable>();

        // Verify dispose doesn't throw
        result.Dispose();
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new CorrelationIdMiddleware(null!));
    }
}
