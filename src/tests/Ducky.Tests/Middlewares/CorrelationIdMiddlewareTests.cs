using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;

namespace Ducky.Tests.Middlewares;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public void InvokeBeforeReduce_WithoutCorrelationId_AssignsNewId()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();
        ActionContext context = new(action);
        Observable<ActionContext> actions = Observable.Return(context);

        Guid? assignedCorrelationId = null;
        eventPublisher.Events
            .OfType<StoreEventArgs, CorrelationIdAssignedEvent>()
            .Subscribe(e => assignedCorrelationId = e.CorrelationId);

        // Act
        Observable<ActionContext> result = middleware.InvokeBeforeReduce(actions);
        result.Subscribe();

        // Assert
        Assert.NotNull(assignedCorrelationId);
        Assert.True(context.Metadata.ContainsKey(CorrelationIdMiddleware.CorrelationIdKey));
        Assert.Equal(assignedCorrelationId, context.Metadata[CorrelationIdMiddleware.CorrelationIdKey]);
    }

    [Fact]
    public void InvokeBeforeReduce_WithExistingCorrelationId_PreservesId()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();
        Guid existingId = Guid.NewGuid();
        ActionContext context = new(action);
        context.Metadata[CorrelationIdMiddleware.CorrelationIdKey] = existingId;
        Observable<ActionContext> actions = Observable.Return(context);

        Guid? publishedCorrelationId = null;
        eventPublisher.Events
            .OfType<StoreEventArgs, CorrelationIdAssignedEvent>()
            .Subscribe(e => publishedCorrelationId = e.CorrelationId);

        // Act
        Observable<ActionContext> result = middleware.InvokeBeforeReduce(actions);
        result.Subscribe();

        // Assert
        Assert.Equal(existingId, publishedCorrelationId);
        Assert.Equal(existingId, context.Metadata[CorrelationIdMiddleware.CorrelationIdKey]);
    }

    [Fact]
    public void InvokeBeforeReduce_PublishesCorrelationIdAssignedEvent()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();
        ActionContext context = new(action);
        Observable<ActionContext> actions = Observable.Return(context);

        CorrelationIdAssignedEvent? publishedEvent = null;
        eventPublisher.Events
            .OfType<StoreEventArgs, CorrelationIdAssignedEvent>()
            .Subscribe(e => publishedEvent = e);

        // Act
        Observable<ActionContext> result = middleware.InvokeBeforeReduce(actions);
        result.Subscribe();

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal(context, publishedEvent.Context);
        Assert.NotEqual(Guid.Empty, publishedEvent.CorrelationId);
    }

    [Fact]
    public void InvokeAfterReduce_PassesThroughActionsUnchanged()
    {
        // Arrange
        StoreEventPublisher eventPublisher = new();
        CorrelationIdMiddleware middleware = new(eventPublisher);
        TestAction action = new();
        ActionContext context = new(action);
        Observable<ActionContext> actions = Observable.Return(context);

        ActionContext? receivedContext = null;

        // Act
        Observable<ActionContext> result = middleware.InvokeAfterReduce(actions);
        result.Subscribe(ctx => receivedContext = ctx);

        // Assert
        Assert.Equal(context, receivedContext);
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CorrelationIdMiddleware(null!));
    }
}
