// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Ducky.Pipeline;

namespace Ducky.Middlewares.ExceptionHandling;

/// <summary>
/// Middleware that provides global exception handling for actions in the pipeline.
/// </summary>
public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly IEnumerable<IExceptionHandler> _exceptionHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="eventPublisher">The store event publisher.</param>
    /// <param name="exceptionHandlers">The collection of exception handlers.</param>
    public ExceptionHandlingMiddleware(
        ILogger<ExceptionHandlingMiddleware> logger,
        IStoreEventPublisher eventPublisher,
        IEnumerable<IExceptionHandler> exceptionHandlers)
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
        _exceptionHandlers = exceptionHandlers;
    }

    /// <inheritdoc />
    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AfterInitializeAllMiddlewares()
    {
        // Nothing to do
    }

    /// <inheritdoc />
    public bool MayDispatchAction(object action)
    {
        return true;
    }

    /// <inheritdoc />
    public void BeforeDispatch(object action)
    {
        // Nothing to do before dispatch
    }

    /// <inheritdoc />
    public void AfterDispatch(object action)
    {
        // Exception handling is now done at a higher level in the pipeline
        // Individual middlewares can still handle their own exceptions
    }

    /// <inheritdoc />
    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { });
    }

    /// <summary>
    /// Handles exceptions that occur during action processing.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="action">The action being processed.</param>
    public void HandleException(Exception exception, object action)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred while processing action {ActionType}",
            action.GetType().Name);

        ActionErrorEventArgs errorEventArgs = new(exception, action, null!);

        // Allow exception handlers to handle the exception
        var isHandled = false;
        foreach (IExceptionHandler handler in _exceptionHandlers)
        {
            try
            {
                if (handler.HandleActionError(errorEventArgs))
                {
                    isHandled = true;
                    break;
                }
            }
            catch (Exception handlerException)
            {
                _logger.LogError(
                    handlerException,
                    "Exception handler {HandlerType} threw an exception while handling {OriginalExceptionType}",
                    handler.GetType().Name,
                    exception.GetType().Name);
            }
        }

        // Publish the error event
        ActionErrorEventArgs finalEventArgs = new(exception, action, null!, isHandled);
        _eventPublisher.Publish(finalEventArgs);
    }
}
