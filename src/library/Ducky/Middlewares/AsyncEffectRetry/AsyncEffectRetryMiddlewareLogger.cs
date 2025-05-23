using Ducky.Pipeline;
using Microsoft.Extensions.Logging;
using R3;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Observes and logs retry events.
/// </summary>
public class AsyncEffectRetryMiddlewareLogger : Observer<PipelineEventArgs>
{
    private readonly ILogger _logger;
    private readonly IDisposable _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncEffectRetryMiddlewareLogger"/> class and subscribes to retry events via R3.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public AsyncEffectRetryMiddlewareLogger(
        ILogger<AsyncEffectRetryMiddlewareLogger> logger,
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
            case RetryAttemptEventArgs retryAttempt:
            {
                _logger.LogWarning(
                    "[RETRY] Attempt {Attempt} for {Action}: {Exception}",
                    retryAttempt.Attempt,
                    retryAttempt.Action.GetType().Name,
                    retryAttempt.Exception.Message);
                break;
            }
            case CircuitBreakerOpenedEventArgs opened:
            {
                _logger.LogWarning(
                    "[CIRCUIT BREAKER] Opened for {Action}: {Exception}",
                    opened.Action.GetType().Name,
                    opened.Exception.Message);
                break;
            }
            case CircuitBreakerResetEventArgs reset:
            {
                _logger.LogWarning(
                    "[CIRCUIT BREAKER] Reset for {Action}",
                    reset.Action.GetType().Name);
                break;
            }
            case ServiceUnavailableEventArgs su:
            {
                _logger.LogWarning(
                    "[SERVICE UNAVAILABLE] {Action}: {Reason}",
                    su.Action.GetType().Name,
                    su.Reason);
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
    protected override void OnCompletedCore(Result result)
    {
        _logger.LogInformation("[EVENT] Event stream completed");
    }

    /// <inheritdoc />
    protected override void DisposeCore()
    {
        _subscription.Dispose();
    }
}
