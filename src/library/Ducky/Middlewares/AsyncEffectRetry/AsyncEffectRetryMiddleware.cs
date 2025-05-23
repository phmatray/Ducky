using Ducky.Pipeline;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Wrap;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Middleware that applies retry and circuit breaker policies to asynchronous actions,
/// and publishes retry/circuit-breaker events to the pipeline event system.
/// </summary>
/// <typeparam name="TState">The type of the state managed by the store.</typeparam>
public sealed class AsyncEffectRetryMiddleware<TState> : StoreMiddleware
    where TState : class
{
    private readonly AsyncPolicyWrap _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncEffectRetryMiddleware{TState}"/> class.
    /// </summary>
    /// <param name="eventPublisher">The event publisher for pipeline events.</param>
    /// <param name="retryDelays">The sequence of retry delays for the retry policy.</param>
    /// <param name="exceptionsAllowedBeforeBreaking">The number of exceptions allowed before opening the circuit breaker.</param>
    /// <param name="durationOfBreak">The duration the circuit breaker remains open.</param>
    public AsyncEffectRetryMiddleware(
        IPipelineEventPublisher eventPublisher,
        TimeSpan[] retryDelays,
        int exceptionsAllowedBeforeBreaking,
        in TimeSpan durationOfBreak)
    {
        AsyncRetryPolicy retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryDelays,
                (exception, timespan, attempt, context) =>
                {
                    var ctx = context["ActionContext"] as IActionContext;
                    object act = context["Action"];
                    Events.Publish(new RetryAttemptEventArgs(ctx!, act!, attempt, exception));
                });

        AsyncCircuitBreakerPolicy breakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking,
                durationOfBreak,
                (exception, duration, context) =>
                {
                    var ctx = context["ActionContext"] as IActionContext;
                    object act = context["Action"];
                    Events.Publish(new CircuitBreakerOpenedEventArgs(ctx!, act!, exception));
                },
                context =>
                {
                    var ctx = context["ActionContext"] as IActionContext;
                    object act = context["Action"];
                    Events.Publish(new CircuitBreakerResetEventArgs(ctx!, act!));
                });

        _policy = Policy.WrapAsync(breakerPolicy, retryPolicy);
    }

    /// <inheritdoc />
    public override StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.FireAndForget;

    /// <inheritdoc />
    public override async Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        if (context.Action is IAsyncEffect asyncAction)
        {
            _ = ExecuteWithPolicyAsync(asyncAction, context, cancellationToken);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the asynchronous effect using the configured retry and circuit breaker policies.
    /// Publishes events for retry attempts, circuit breaker state changes, and service unavailability.
    /// </summary>
    /// <typeparam name="TAction">The type of the action being dispatched.</typeparam>
    /// <param name="asyncEffect">The asynchronous effect to execute.</param>
    /// <param name="context">The action context.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task ExecuteWithPolicyAsync<TAction>(
        IAsyncEffect asyncEffect,
        ActionContext<TAction> context,
        CancellationToken cancellationToken)
    {
        try
        {
            Context pollyContext = new()
            {
                ["ActionContext"] = context,
                ["Action"] = context.Action!
            };

            await _policy
                .ExecuteAsync(
                    async _ => await asyncEffect
                        .HandleAsync(context.Action!, Store.CurrentState)
                        .ConfigureAwait(false),
                    pollyContext)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ServiceUnavailableAction unavailable = new(ex.Message, context.Action!);
            Events.Publish(new ServiceUnavailableEventArgs(context, context.Action!, ex.Message, ex));
            Dispatcher.Dispatch(unavailable);
        }
    }
}
