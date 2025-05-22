using Ducky.Pipeline;
using Microsoft.Extensions.Logging;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Observes retry events and logs them.
/// </summary>
public class AsyncEffectRetryEventListener
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncEffectRetryEventListener"/> class and subscribes to retry events.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="eventPublisher">The pipeline event publisher.</param>
    public AsyncEffectRetryEventListener(
        ILogger<AsyncEffectRetryEventListener> logger,
        IPipelineEventPublisher eventPublisher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        eventPublisher.EventPublished += OnEventPublished;
    }

    private void OnEventPublished(object? sender, PipelineEventArgs e)
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
}
