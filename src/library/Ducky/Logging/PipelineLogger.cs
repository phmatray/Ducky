using Ducky.Pipeline;
using Microsoft.Extensions.Logging;

namespace Ducky;

/// <summary>
/// Observes and logs pipeline events.
/// </summary>
public class PipelineLogger
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineLogger"/> class and subscribes to pipeline events.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public PipelineLogger(
        ILogger<PipelineLogger> logger,
        IPipelineEventPublisher eventPublisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        eventPublisher.EventPublished += OnEventPublished;
    }

    /// <summary>
    /// Handles and logs pipeline events to the console.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The pipeline event.</param>
    private void OnEventPublished(object? sender, PipelineEventArgs e)
    {
        switch (e)
        {
            case ActionStartedEventArgs started:
            {
                _logger.LogInformation(
                    "[EVENT] Action started: {ActionType}",
                    started.Context.ActionType.Name);
                break;
            }
            case ActionCompletedEventArgs completed:
            {
                _logger.LogInformation(
                    "[EVENT] Action completed: {ActionType}",
                    completed.Context.ActionType.Name);
                break;
            }
            case MiddlewareStartedEvent startedMw:
            {
                _logger.LogInformation(
                    "[EVENT] Middleware {Phase} started: {Middleware} for {ActionType}",
                    startedMw.Phase,
                    startedMw.Middleware.GetType().Name,
                    startedMw.Context.ActionType.Name);
                break;
            }
            case MiddlewareCompletedEventArgs completedMw:
            {
                _logger.LogInformation(
                    "[EVENT] Middleware {Phase} completed: {Middleware} for {ActionType}",
                    completedMw.Phase,
                    completedMw.Middleware.GetType().Name,
                    completedMw.Context.ActionType.Name);
                break;
            }
            case MiddlewareErroredEventArgs erroredMw:
            {
                _logger.LogError(
                    "[EVENT] Middleware {Phase} error: {Middleware}: {Exception}",
                    erroredMw.Phase,
                    erroredMw.Middleware.GetType().Name,
                    erroredMw.Exception);
                break;
            }
            case ActionAbortedEventArgs aborted:
            {
                _logger.LogWarning(
                    "[EVENT] Action aborted: {ActionType}, Reason: {Reason}",
                    aborted.Context.ActionType.Name,
                    aborted.Reason);
                break;
            }
        }
    }
}
