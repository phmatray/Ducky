using Ducky.Pipeline;
using Microsoft.Extensions.Logging;
using R3;

namespace Ducky;

/// <summary>
/// Observes and logs pipeline events.
/// </summary>
public class PipelineLogger : Observer<PipelineEventArgs>
{
    private readonly ILogger _logger;
    private readonly IDisposable _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineLogger"/> class and subscribes to pipeline events via R3.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public PipelineLogger(
        ILogger<PipelineLogger> logger,
        IPipelineEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventPublisher);

        _logger = logger;
        _subscription = eventPublisher.Events.Subscribe(this);
    }

    /// <inheritdoc />
    protected override void OnNextCore(PipelineEventArgs e)
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

    /// <inheritdoc />
    protected override void OnErrorResumeCore(Exception error)
    {
        _logger.LogError(error, "[EVENT] Error in event stream");
    }

    /// <inheritdoc />
    protected override void OnCompletedCore(R3.Result result)
    {
        _logger.LogInformation("[EVENT] Event stream completed");
    }

    /// <inheritdoc />
    protected override void DisposeCore()
    {
        _subscription.Dispose();
    }
}
