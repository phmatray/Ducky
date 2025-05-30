// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Pipeline;

namespace Demo.BlazorWasm.Features.Feedback;

/// <summary>
/// Exception handler that creates notifications for errors in the store.
/// </summary>
public class NotificationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<NotificationExceptionHandler> _logger;
    private readonly IDispatcher _dispatcher;

    public NotificationExceptionHandler(
        ILogger<NotificationExceptionHandler> logger,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public bool HandleActionError(ActionErrorEventArgs eventArgs)
    {
        _logger.LogError(
            eventArgs.Exception,
            "Error occurred while processing action {ActionType}",
            eventArgs.Action.GetType().Name);

        // Create a notification for the error
        ExceptionNotification notification = new(eventArgs.Exception);
        _dispatcher.Dispatch(new AddNotification(notification));

        // Return true to indicate the error has been handled
        // This prevents it from propagating further
        return true;
    }

    public bool HandleEffectError(EffectErrorEventArgs eventArgs)
    {
        _logger.LogError(
            eventArgs.Exception,
            "Error occurred in effect {EffectType}",
            eventArgs.EffectType.Name);

        // Create a notification for the error
        ExceptionNotification notification = new(eventArgs.Exception);
        _dispatcher.Dispatch(new AddNotification(notification));

        // Return true to indicate the error has been handled
        return true;
    }
}
