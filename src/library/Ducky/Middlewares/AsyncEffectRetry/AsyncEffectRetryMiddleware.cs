using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using R3;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Middleware that adds retry and circuit breaker capabilities to async effects.
/// </summary>
public sealed class AsyncEffectRetryMiddleware : IActionMiddleware
{
    private readonly ILogger<AsyncEffectRetryMiddleware> _logger;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly Func<IRootState> _getState;
    private readonly IDispatcher _dispatcher;
    private readonly IAsyncEffect[] _effects;
    private readonly Dictionary<Type, ResiliencePipeline> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncEffectRetryMiddleware"/> class.
    /// </summary>
    public AsyncEffectRetryMiddleware(
        ILogger<AsyncEffectRetryMiddleware> logger,
        IStoreEventPublisher eventPublisher,
        IServiceProvider services,
        Func<IRootState> getState,
        IDispatcher dispatcher)
    {
        _logger = logger;
        _eventPublisher = eventPublisher;
        _getState = getState;
        _dispatcher = dispatcher;
        _policies = new Dictionary<Type, ResiliencePipeline>();

        // Resolve and cache effects
        _effects = services.GetServices<IAsyncEffect>().ToArray();

        // Inject the dispatcher into each effect
        foreach (IAsyncEffect effect in _effects)
        {
            effect.SetDispatcher(dispatcher);
        }

        // Initialize policies for each effect type
        InitializePolicies();
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        // Handle async effects with retry and circuit breaker
        return actions.Do(ctx =>
        {
            foreach (IAsyncEffect effect in _effects)
            {
                if (effect.CanHandle(ctx.Action))
                {
                    ExecuteWithRetry(effect, ctx);
                }
            }
        });
    }

    private void InitializePolicies()
    {
        foreach (IAsyncEffect effect in _effects)
        {
            Type effectType = effect.GetType();
            
            ResiliencePipelineBuilder pipelineBuilder = new();
            
            // Add retry policy
            pipelineBuilder.AddRetry(new()
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _eventPublisher.Publish(new RetryAttemptEventArgs(
                        new ActionContext(effectType),
                        effectType,
                        args.AttemptNumber,
                        args.Outcome.Exception!));
                    
                    _logger.LogWarning(
                        "Retry attempt {AttemptNumber} for effect {EffectType}: {Exception}",
                        args.AttemptNumber,
                        effectType.Name,
                        args.Outcome.Exception?.Message);
                    
                    return default;
                }
            });
            
            // Add circuit breaker policy
            pipelineBuilder.AddCircuitBreaker(new()
            {
                FailureRatio = 0.5,
                MinimumThroughput = 2,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                OnOpened = args =>
                {
                    _eventPublisher.Publish(new CircuitBreakerOpenedEventArgs(
                        new ActionContext(effectType),
                        effectType,
                        args.Outcome.Exception!));
                    
                    _logger.LogError(
                        "Circuit breaker opened for effect {EffectType}: {Exception}",
                        effectType.Name,
                        args.Outcome.Exception?.Message);
                    
                    return default;
                },
                OnClosed = _ =>
                {
                    _eventPublisher.Publish(new CircuitBreakerResetEventArgs(
                        new ActionContext(effectType),
                        effectType));
                    
                    _logger.LogInformation(
                        "Circuit breaker closed for effect {EffectType}",
                        effectType.Name);
                    
                    return default;
                }
            });

            _policies[effectType] = pipelineBuilder.Build();
        }
    }

    private void ExecuteWithRetry(IAsyncEffect effect, ActionContext context)
    {
        Type effectType = effect.GetType();
        ResiliencePipeline pipeline = _policies[effectType];

        _ = Task.Run(async () =>
        {
            try
            {
                await pipeline
                    .ExecuteAsync(async _ =>
                    {
                        await effect.HandleAsync(context.Action, _getState()).ConfigureAwait(false);
                    })
                    .ConfigureAwait(false);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(
                    "Circuit breaker is open for effect {EffectType}, dispatching ServiceUnavailableAction",
                    effectType.Name);

                _dispatcher.Dispatch(new ServiceUnavailableAction(
                    $"Circuit breaker is open for {effectType.Name}",
                    context.Action));

                _eventPublisher.Publish(new ServiceUnavailableEventArgs(
                    context,
                    context.Action,
                    "Circuit breaker is open",
                    ex));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "All retry attempts failed for effect {EffectType}, dispatching ServiceUnavailableAction",
                    effectType.Name);

                _dispatcher.Dispatch(new ServiceUnavailableAction(
                    $"All retry attempts failed for {effectType.Name}: {ex.Message}",
                    context.Action));

                _eventPublisher.Publish(new ServiceUnavailableEventArgs(
                    context,
                    context.Action,
                    "All retry attempts failed",
                    ex));
            }
        });
    }

}
