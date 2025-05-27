// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Ducky.Abstractions;
using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.ExceptionHandling;

/// <summary>
/// Middleware that provides global exception handling for actions in the pipeline.
/// </summary>
public sealed class ExceptionHandlingMiddleware : IActionMiddleware
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
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions.SelectMany(context =>
        {
            return Observable.Return(context)
                .Catch<ActionContext, Exception>(exception =>
                {
                    HandleException(exception, context);

                    // Mark context as aborted so it doesn't continue processing
                    context.Abort();
                    return Observable.Return(context);
                });
        });
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions.SelectMany(context =>
        {
            return Observable.Return(context)
                .Catch<ActionContext, Exception>(exception =>
                {
                    HandleException(exception, context);
                    return Observable.Return(context);
                });
        });
    }

    private void HandleException(Exception exception, ActionContext context)
    {
        _logger.LogError(
            exception,
            "Unhandled exception occurred while processing action {ActionType}",
            context.Action.GetType().Name);

        ActionErrorEventArgs errorEventArgs = new(exception, context.Action, context);

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
        ActionErrorEventArgs finalEventArgs = new(exception, context.Action, context, isHandled);
        _eventPublisher.Publish(finalEventArgs);
    }
}
