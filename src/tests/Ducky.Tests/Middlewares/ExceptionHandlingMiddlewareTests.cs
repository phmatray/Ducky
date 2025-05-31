// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Moq;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Pipeline;

namespace Ducky.Tests.Middlewares;

public sealed class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _logger;
    private readonly Mock<IStoreEventPublisher> _eventPublisher;
    private readonly List<IExceptionHandler> _exceptionHandlers;
    private readonly ExceptionHandlingMiddleware _middleware;
    private readonly Mock<IDispatcher> _dispatcher;
    private readonly Mock<IStore> _store;

    public ExceptionHandlingMiddlewareTests()
    {
        _logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _eventPublisher = new Mock<IStoreEventPublisher>();
        _exceptionHandlers = new List<IExceptionHandler>();
        _middleware = new ExceptionHandlingMiddleware(_logger.Object, _eventPublisher.Object, _exceptionHandlers);
        _dispatcher = new Mock<IDispatcher>();
        _store = new Mock<IStore>();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCompleteSuccessfully()
    {
        // Act
        await _middleware.InitializeAsync(_dispatcher.Object, _store.Object);

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void AfterInitializeAllMiddlewares_ShouldCompleteSuccessfully()
    {
        // Act
        _middleware.AfterInitializeAllMiddlewares();

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void MayDispatchAction_ShouldAlwaysReturnTrue()
    {
        // Arrange
        TestAction action = new();

        // Act
        bool result = _middleware.MayDispatchAction(action);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void BeforeDispatch_ShouldCompleteSuccessfully()
    {
        // Arrange
        TestAction action = new();

        // Act
        _middleware.BeforeDispatch(action);

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void AfterDispatch_ShouldCompleteSuccessfully()
    {
        // Arrange
        TestAction action = new();

        // Act
        _middleware.AfterDispatch(action);

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void BeginInternalMiddlewareChange_ShouldReturnDisposable()
    {
        // Act
        IDisposable result = _middleware.BeginInternalMiddlewareChange();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IDisposable>(result);

        // Verify dispose doesn't throw
        result.Dispose();
    }

    [Fact]
    public void HandleException_WithNoHandlers_ShouldLogAndPublishUnhandledError()
    {
        // Arrange
        InvalidOperationException exception = new("Test exception");
        TestAction action = new();

        // Act
        _middleware.HandleException(exception, action);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unhandled exception occurred")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _eventPublisher.Verify(
            x => x.Publish(It.Is<ActionErrorEventArgs>(args =>
                args.Exception == exception
                    && args.Action.Equals(action)
                    && !args.IsHandled)),
            Times.Once);
    }

    [Fact]
    public void HandleException_WithHandlerThatHandles_ShouldMarkAsHandled()
    {
        // Arrange
        InvalidOperationException exception = new("Test exception");
        TestAction action = new();
        TestExceptionHandler testHandler = new() { ShouldHandleActionErrors = true };
        _exceptionHandlers.Add(testHandler);

        ExceptionHandlingMiddleware middleware = new(_logger.Object, _eventPublisher.Object, _exceptionHandlers);

        // Act
        middleware.HandleException(exception, action);

        // Assert
        _eventPublisher.Verify(
            x => x.Publish(It.Is<ActionErrorEventArgs>(args =>
                args.Exception == exception
                    && args.Action.Equals(action)
                    && args.IsHandled)),
            Times.Once);
    }

    [Fact]
    public void HandleException_WhenHandlerThrows_ShouldLogHandlerError()
    {
        // Arrange
        InvalidOperationException exception = new("Test exception");
        TestAction action = new();
        Exception handlerException = new("Handler error");

        Mock<IExceptionHandler> mockHandler = new();
        mockHandler.Setup(h => h.HandleActionError(It.IsAny<ActionErrorEventArgs>()))
            .Throws(handlerException);
        _exceptionHandlers.Add(mockHandler.Object);

        ExceptionHandlingMiddleware middleware = new(_logger.Object, _eventPublisher.Object, _exceptionHandlers);

        // Act
        middleware.HandleException(exception, action);

        // Assert
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Exception handler") && v.ToString()!.Contains("threw an exception")),
                handlerException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
