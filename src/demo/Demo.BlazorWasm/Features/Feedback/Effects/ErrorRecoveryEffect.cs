// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles retry of failed operations.
/// </summary>
public class RetryFailedOperationEffect(
    ILogger<RetryFailedOperationEffect> logger)
    : AsyncEffect<RetryFailedOperation>
{
    public override Task HandleAsync(RetryFailedOperation action, IRootState rootState)
    {
        logger.LogInformation(
            "Retrying failed operation: {ActionType}",
            action.OriginalAction.GetType().Name);

        // Dispatch the original action again
        Dispatcher.Dispatch(action.OriginalAction);

        // Add a notification about the retry
        InfoNotification notification = new(
            $"Retrying {action.OriginalAction.GetType().Name}...");
        Dispatcher.AddNotification(notification);

        return Task.CompletedTask;
    }
}

/// <summary>
/// Effect that handles error reporting.
/// </summary>
public class ReportErrorEffect(
    ILogger<ReportErrorEffect> logger)
    : AsyncEffect<ReportError>
{
    public override Task HandleAsync(ReportError action, IRootState rootState)
    {
        logger.LogError(
            action.Exception,
            "Error reported by user. Feedback: {UserFeedback}",
            action.UserFeedback ?? "No feedback provided");

        // In a real application, you would send this to an external logging service
        // For demo purposes, we'll just add a notification
        SuccessNotification notification = new(
            "Error report sent successfully. Thank you for your feedback!");
        Dispatcher.AddNotification(notification);

        return Task.CompletedTask;
    }
}
