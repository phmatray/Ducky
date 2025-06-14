using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using System.Reflection;

namespace Ducky.Builder;

/// <summary>
/// A simplified builder for configuring Ducky services with sensible defaults.
/// </summary>
public class DuckyBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _middlewareTypes = [];
    private readonly HashSet<Assembly> _assembliesToScan = [];
    private bool _defaultMiddlewaresAdded;
    private Action<DuckyOptions>? _configureOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public DuckyBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        // Add entry assembly by default
        if (Assembly.GetEntryAssembly() is not { } entryAssembly)
        {
            return;
        }

        _assembliesToScan.Add(entryAssembly);
    }

    /// <summary>
    /// Uses the default middleware configuration (CorrelationId, AsyncEffect).
    /// </summary>
    public DuckyBuilder UseDefaultMiddlewares()
    {
        if (_defaultMiddlewaresAdded)
        {
            return this;
        }

        _defaultMiddlewaresAdded = true;
        AddMiddleware<CorrelationIdMiddleware>();
        AddMiddleware<AsyncEffectMiddleware>();
        return this;
    }

    /// <summary>
    /// Uses a production-ready middleware configuration.
    /// </summary>
    public DuckyBuilder UseProductionPreset()
    {
        UseDefaultMiddlewares();
        // Add other production middlewares as needed
        return this;
    }

    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    public DuckyBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IMiddleware
    {
        Type middlewareType = typeof(TMiddleware);
        if (!_middlewareTypes.Contains(middlewareType))
        {
            _middlewareTypes.Add(middlewareType);
            _services.AddScoped<TMiddleware>();
            _services.AddScoped<IMiddleware>(sp => sp.GetRequiredService<TMiddleware>());
        }

        return this;
    }

    /// <summary>
    /// Adds a middleware by type (for dynamic scenarios).
    /// </summary>
    public DuckyBuilder AddMiddleware(Type middlewareType)
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
            _services.AddScoped(middlewareType);
            _services.AddScoped(typeof(IMiddleware), middlewareType);
        }

        return this;
    }

    /// <summary>
    /// Adds a state slice to the store.
    /// </summary>
    public DuckyBuilder AddSlice<TState>() where TState : class, IState, new()
    {
        _services.AddScoped<ISlice<TState>, SliceReducers<TState>>();
        return this;
    }

    /// <summary>
    /// Adds an async effect.
    /// </summary>
    public DuckyBuilder AddEffect<TEffect>() where TEffect : class, IAsyncEffect
    {
        // Auto-add AsyncEffectMiddleware if adding effects
        if (!_middlewareTypes.Contains(typeof(AsyncEffectMiddleware)))
        {
            AddMiddleware<AsyncEffectMiddleware>();
        }

        _services.AddScoped<TEffect>();
        _services.AddScoped<IAsyncEffect>(sp => sp.GetRequiredService<TEffect>());
        return this;
    }

    /// <summary>
    /// Adds an async effect group that contains multiple related effects.
    /// </summary>
    public DuckyBuilder AddEffectGroup<TEffectGroup>() where TEffectGroup : AsyncEffectGroup
    {
        // Auto-add AsyncEffectMiddleware if adding effects
        if (!_middlewareTypes.Contains(typeof(AsyncEffectMiddleware)))
        {
            AddMiddleware<AsyncEffectMiddleware>();
        }

        _services.AddScoped<TEffectGroup>();
        _services.AddScoped<IAsyncEffect>(sp => sp.GetRequiredService<TEffectGroup>());
        return this;
    }

    /// <summary>
    /// Adds an exception handler.
    /// </summary>
    public DuckyBuilder AddExceptionHandler<THandler>() where THandler : class, IExceptionHandler
    {
        _services.AddScoped<THandler>();
        _services.AddScoped<IExceptionHandler>(sp => sp.GetRequiredService<THandler>());
        return this;
    }

    /// <summary>
    /// Scans assemblies for slices and effects. Entry assembly is scanned by default.
    /// </summary>
    public DuckyBuilder ScanAssemblies(params string[] assemblyNames)
    {
        foreach (string assemblyName in assemblyNames)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                _assembliesToScan.Add(assembly);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load assembly '{assemblyName}'", ex);
            }
        }

        return this;
    }

    /// <summary>
    /// Scans assemblies for slices and effects.
    /// </summary>
    public DuckyBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            _assembliesToScan.Add(assembly);
        }

        return this;
    }


    /// <summary>
    /// Configures advanced store options.
    /// </summary>
    public DuckyBuilder ConfigureStore(Action<DuckyOptions> configure)
    {
        _configureOptions = configure;
        return this;
    }

    /// <summary>
    /// Builds and registers all Ducky services.
    /// </summary>
    internal IServiceCollection Build()
    {
        // Validate middleware order
        List<MiddlewareOrderViolation> violations = MiddlewareOrderValidator.Validate(_middlewareTypes);
        if (violations.Count > 0)
        {
            throw new MiddlewareOrderException(violations);
        }

        // Register core services
        _services.AddScoped<IDispatcher, Dispatcher>();
        _services.AddScoped<IStoreEventPublisher, StoreEventPublisher>();
        _services.AddScoped<IStateSerializer, RootStateSerializer>();
        _services.AddScoped<DuckyStoreLogger>();

        // Scan assemblies for slices and effects
        foreach (Assembly assembly in _assembliesToScan)
        {
            ScanAndRegister<ISlice>(assembly);
            ScanAndRegister<IAsyncEffect>(assembly);
        }

        // Configure store
        _services.AddScoped<IStore, DuckyStore>(sp =>
        {
            IDispatcher dispatcher = sp.GetRequiredService<IDispatcher>();
            IStoreEventPublisher eventPublisher = sp.GetRequiredService<IStoreEventPublisher>();
            IEnumerable<ISlice> slices = sp.GetServices<ISlice>();
            ILogger<ActionPipeline> logger = sp.GetRequiredService<ILogger<ActionPipeline>>();

            // Check for effects and validate middleware requirements
            IEnumerable<IAsyncEffect> effects = sp.GetServices<IAsyncEffect>();
            if (effects.Any() && !_middlewareTypes.Contains(typeof(AsyncEffectMiddleware)))
            {
                throw new MissingMiddlewareException(
                    "AsyncEffectMiddleware is required when using async effects. Add it with builder.AddMiddleware<AsyncEffectMiddleware>()");
            }

            // Create pipeline
            ActionPipeline pipeline = new(sp, logger);

            // Configure pipeline with middlewares
            for (int i = 0; i < _middlewareTypes.Count; i++)
            {
                Type middlewareType = _middlewareTypes[i];
                pipeline.Use(middlewareType);
            }

            // Create store
            _ = sp.GetRequiredService<DuckyStoreLogger>(); // Ensure logger is created
            return new DuckyStore(dispatcher, pipeline, eventPublisher, slices);
        });

        // Apply any additional configuration
        if (_configureOptions is not null)
        {
            _services.AddSingleton(_configureOptions);
        }

        return _services;
    }

    private void ScanAndRegister<T>(Assembly assembly)
    {
        IEnumerable<ServiceDescriptor> serviceDescriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(T).IsAssignableFrom(type))
            .Select(type => ServiceDescriptor.Scoped(typeof(T), type));

        _services.TryAddEnumerable(serviceDescriptors);
    }
}
