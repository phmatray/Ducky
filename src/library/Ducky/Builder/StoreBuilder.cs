using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;

namespace Ducky.Builder;

internal class StoreBuilder : IStoreBuilder
{
    private readonly List<Type> _middlewareTypes = [];
    private readonly List<Type> _sliceTypes = [];
    private bool _asyncEffectMiddlewareAdded;
    private bool _reactiveEffectMiddlewareAdded;
    private bool _validationEnabled = true;

    public IServiceCollection Services { get; }

    public StoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IStoreBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IActionMiddleware
    {
        Type middlewareType = typeof(TMiddleware);

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.TryAddScoped<TMiddleware>();
            Services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<TMiddleware>());

            // Track effect middleware additions
            if (middlewareType == typeof(AsyncEffectMiddleware))
            {
                _asyncEffectMiddlewareAdded = true;
            }
            else if (middlewareType == typeof(ReactiveEffectMiddleware))
            {
                _reactiveEffectMiddlewareAdded = true;
            }
        }

        return this;
    }

    public IStoreBuilder AddMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> implementationFactory)
        where TMiddleware : class, IActionMiddleware
    {
        Type middlewareType = typeof(TMiddleware);

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.TryAddScoped(implementationFactory);
            Services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<TMiddleware>());

            // Track effect middleware additions
            if (middlewareType == typeof(AsyncEffectMiddleware))
            {
                _asyncEffectMiddlewareAdded = true;
            }
            else if (middlewareType == typeof(ReactiveEffectMiddleware))
            {
                _reactiveEffectMiddlewareAdded = true;
            }
        }

        return this;
    }

    public IStoreBuilder AddMiddleware(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        if (!typeof(IActionMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException(
                $"Type {middlewareType.Name} must implement IActionMiddleware",
                nameof(middlewareType));
        }

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.TryAddScoped(middlewareType);
            Services.AddScoped(typeof(IActionMiddleware), middlewareType);
        }

        return this;
    }

    public IStoreBuilder AddMiddleware(Type middlewareType, Func<IServiceProvider, object> implementationFactory)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        if (!typeof(IActionMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException(
                $"Type {middlewareType.Name} must implement IActionMiddleware",
                nameof(middlewareType));
        }

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.TryAddScoped(middlewareType, implementationFactory);
            Services.AddScoped<IActionMiddleware>(sp => (IActionMiddleware)implementationFactory(sp));
        }

        return this;
    }

    public IStoreBuilder ConfigureOptions(Action<DuckyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        // For now, store the configuration action - it will be applied when building the store
        Services.AddSingleton(configureOptions);
        return this;
    }

    public IStoreBuilder AddSlice<TState>() where TState : class, IState, new()
    {
        Type sliceType = typeof(ISlice<TState>);

        if (!_sliceTypes.Contains(sliceType))
        {
            _sliceTypes.Add(sliceType);
            Services.TryAddScoped<ISlice<TState>, SliceReducers<TState>>();
        }

        return this;
    }

    public IStoreBuilder AddSlice<TState>(Func<IServiceProvider, TState> stateFactory) where TState : class, IState
    {
        ArgumentNullException.ThrowIfNull(stateFactory);

        Type sliceType = typeof(ISlice<TState>);

        if (!_sliceTypes.Contains(sliceType))
        {
            _sliceTypes.Add(sliceType);
            // For now, we'll just register the slice type - actual implementation will be provided by the user
            // The SliceReducers is an abstract class, so users need to create their own implementation
            Services.TryAddScoped(sliceType);
        }

        return this;
    }

    public IStoreBuilder AddEffect<TEffect>() where TEffect : class, IAsyncEffect
    {
        if (!_asyncEffectMiddlewareAdded)
        {
            throw new MissingMiddlewareException(
                typeof(TEffect),
                typeof(AsyncEffectMiddleware),
                "AddAsyncEffectMiddleware()");
        }

        Services.TryAddScoped<TEffect>();
        Services.AddScoped<IAsyncEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    public IStoreBuilder AddEffect<TEffect>(Func<IServiceProvider, TEffect> implementationFactory)
        where TEffect : class, IAsyncEffect
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);

        Services.TryAddScoped(implementationFactory);
        Services.AddScoped<IAsyncEffect>(sp => implementationFactory(sp));
        return this;
    }

    public IStoreBuilder AddReactiveEffect<TEffect>() where TEffect : class, IReactiveEffect
    {
        if (!_reactiveEffectMiddlewareAdded)
        {
            throw new MissingMiddlewareException(
                typeof(TEffect),
                typeof(ReactiveEffectMiddleware),
                "AddReactiveEffectMiddleware()");
        }

        Services.TryAddScoped<TEffect>();
        Services.AddScoped<IReactiveEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    public IStoreBuilder AddReactiveEffect<TEffect>(Func<IServiceProvider, TEffect> implementationFactory)
        where TEffect : class, IReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);

        Services.TryAddScoped(implementationFactory);
        Services.AddScoped<IReactiveEffect>(sp => implementationFactory(sp));
        return this;
    }

    public IStoreBuilder AddExceptionHandler<TExceptionHandler>() where TExceptionHandler : class, IExceptionHandler
    {
        Services.TryAddScoped<TExceptionHandler>();
        Services.AddScoped<IExceptionHandler>(sp => sp.GetRequiredService<TExceptionHandler>());
        return this;
    }

    public IStoreBuilder AddExceptionHandler<TExceptionHandler>(
        Func<IServiceProvider, TExceptionHandler> implementationFactory)
        where TExceptionHandler : class, IExceptionHandler
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);

        Services.TryAddScoped(implementationFactory);
        Services.AddScoped<IExceptionHandler>(implementationFactory);
        return this;
    }

    internal List<Type> GetMiddlewareTypes()
    {
        if (_validationEnabled)
        {
            List<MiddlewareOrderViolation> violations = MiddlewareOrderValidator.Validate(_middlewareTypes);
            if (violations.Count > 0)
            {
                throw new MiddlewareOrderException(violations);
            }
        }

        return new List<Type>(_middlewareTypes);
    }

    /// <summary>
    /// Disables middleware order validation (useful for testing or advanced scenarios).
    /// </summary>
    public IStoreBuilder DisableOrderValidation()
    {
        _validationEnabled = false;
        return this;
    }
}
