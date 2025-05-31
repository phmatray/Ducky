// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.ReactiveEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that handles error recovery actions.
/// </summary>
public class ErrorRecoveryEffect : ReactiveEffect
{
    private readonly ILogger<ErrorRecoveryEffect> _logger;
    private readonly IDispatcher _dispatcher;

    public ErrorRecoveryEffect(
        ILogger<ErrorRecoveryEffect> logger,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        Observable<RetryFailedOperation> retryEffects = actions.OfType<object, RetryFailedOperation>()
            .Do(action =>
            {
                _logger.LogInformation(
                    "Retrying failed operation: {ActionType}",
                    action.OriginalAction.GetType().Name);

                // Dispatch the original action again
                _dispatcher.Dispatch(action.OriginalAction);

                // Add a notification about the retry
                InfoNotification notification = new(
                    $"Retrying {action.OriginalAction.GetType().Name}...");
                _dispatcher.Dispatch(new AddNotification(notification));
            });

        Observable<ReportError> errorReportEffects = actions.OfType<object, ReportError>()
            .Do(action =>
            {
                _logger.LogError(
                    action.Exception,
                    "Error reported by user. Feedback: {UserFeedback}",
                    action.UserFeedback ?? "No feedback provided");

                // In a real application, you would send this to an external logging service
                // For demo purposes, we'll just add a notification
                SuccessNotification notification = new(
                    "Error report sent successfully. Thank you for your feedback!");
                _dispatcher.Dispatch(new AddNotification(notification));
            });

        return Observable
            .Merge(
                retryEffects.Cast<RetryFailedOperation, object>(),
                errorReportEffects.Cast<ReportError, object>())
            .IgnoreElements();
    }
}
