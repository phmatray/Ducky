using Microsoft.Extensions.DependencyInjection;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;

namespace Ducky.Builder;

internal class StoreBuilder : IStoreBuilder
{
    private readonly List<Type> _middlewareTypes = [];
    private readonly List<Type> _sliceTypes = [];
    private bool _asyncEffectMiddlewareAdded;
    private bool _validationEnabled = true;

    public IServiceCollection Services { get; }

    public StoreBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IStoreBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IMiddleware
    {
        Type middlewareType = typeof(TMiddleware);

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.AddScoped<TMiddleware>();
            Services.AddScoped<IMiddleware>(sp => sp.GetRequiredService<TMiddleware>());

            TrackEffectMiddleware(middlewareType);
        }

        return this;
    }

    public IStoreBuilder AddMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> implementationFactory)
        where TMiddleware : class, IMiddleware
    {
        Type middlewareType = typeof(TMiddleware);

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.AddScoped(implementationFactory);
            Services.AddScoped<IMiddleware>(sp => sp.GetRequiredService<TMiddleware>());

            TrackEffectMiddleware(middlewareType);
        }

        return this;
    }

    public IStoreBuilder AddMiddleware(Type middlewareType)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);

        if (!typeof(IMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException(
                $"Type {middlewareType.Name} must implement IMiddleware",
                nameof(middlewareType));
        }

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.AddScoped(middlewareType);
            Services.AddScoped(typeof(IMiddleware), middlewareType);
        }

        return this;
    }

    public IStoreBuilder AddMiddleware(Type middlewareType, Func<IServiceProvider, object> implementationFactory)
    {
        ArgumentNullException.ThrowIfNull(middlewareType);
        ArgumentNullException.ThrowIfNull(implementationFactory);

        if (!typeof(IMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException(
                $"Type {middlewareType.Name} must implement IMiddleware",
                nameof(middlewareType));
        }

        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            Services.AddScoped(middlewareType, implementationFactory);
            Services.AddScoped<IMiddleware>(sp => (IMiddleware)implementationFactory(sp));
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
            Services.AddScoped<ISlice<TState>, SliceReducers<TState>>();
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
            Services.AddScoped(sliceType);
        }

        return this;
    }

    public IStoreBuilder AddEffect<TEffect>() where TEffect : class, IAsyncEffect
    {
        EnsureMiddlewareAdded(_asyncEffectMiddlewareAdded, typeof(AsyncEffectMiddleware), "AddAsyncEffectMiddleware()");
        RegisterService<TEffect, IAsyncEffect>();
        return this;
    }

    public IStoreBuilder AddEffect<TEffect>(Func<IServiceProvider, TEffect> implementationFactory)
        where TEffect : class, IAsyncEffect
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        EnsureMiddlewareAdded(_asyncEffectMiddlewareAdded, typeof(AsyncEffectMiddleware), "AddAsyncEffectMiddleware()");
        RegisterService(implementationFactory, sp => (IAsyncEffect)implementationFactory(sp));
        return this;
    }

    public IStoreBuilder AddExceptionHandler<TExceptionHandler>() where TExceptionHandler : class, IExceptionHandler
    {
        RegisterService<TExceptionHandler, IExceptionHandler>();
        return this;
    }

    public IStoreBuilder AddExceptionHandler<TExceptionHandler>(
        Func<IServiceProvider, TExceptionHandler> implementationFactory)
        where TExceptionHandler : class, IExceptionHandler
    {
        ArgumentNullException.ThrowIfNull(implementationFactory);
        RegisterService(implementationFactory, sp => (IExceptionHandler)implementationFactory(sp));
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

    private void EnsureMiddlewareAdded(bool isAdded, Type middlewareType, string methodName)
    {
        if (isAdded)
        {
            return;
        }

        throw new MissingMiddlewareException(
            typeof(object), // This will be replaced by the actual effect type in the exception
            middlewareType,
            methodName);
    }

    private void RegisterService<TImplementation, TService>()
        where TImplementation : class, TService
        where TService : class
    {
        Services.AddScoped<TImplementation>();
        Services.AddScoped<TService>(sp => sp.GetRequiredService<TImplementation>());
    }

    private void RegisterService<TService>(
        Func<IServiceProvider, TService> implementationFactory,
        Func<IServiceProvider, TService> serviceFactory)
        where TService : class
    {
        Services.AddScoped(implementationFactory);
        Services.AddScoped(serviceFactory);
    }

    private void TrackEffectMiddleware(Type middlewareType)
    {
        if (middlewareType != typeof(AsyncEffectMiddleware))
        {
            return;
        }

        _asyncEffectMiddlewareAdded = true;
    }
}
