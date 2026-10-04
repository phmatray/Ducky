using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky;

/// <summary>
/// Configures the store inside <see cref="DuckyServiceCollectionExtensions.AddDucky"/>. Nothing here throws a
/// configuration error: every problem is reported together at the first store resolution.
/// </summary>
public sealed class DuckyBuilder
{
    private readonly HashSet<Type> _sliceTypes = [];
    private readonly List<DuckyConfig.SliceRegistration> _slices = [];
    private readonly List<DuckyError> _sliceErrors = [];
    private readonly List<(string Source, Exception Thrown)> _sliceFailures = [];
    private readonly List<Func<IServiceProvider, IEnumerable<DuckyError>>> _rules = [];
    private readonly HashSet<Type> _middlewareTypes = [];
    private readonly List<Func<IServiceProvider, Middleware>> _middleware = [];
    private readonly OrderedDictionary<Type, DuckyConfig.EffectRegistration> _effects = [];
    private readonly List<CtorCheck.Requirement> _ctorChecks = [];
    private ServiceLifetime? _lifetime;

    internal DuckyBuilder(IServiceCollection services) => Services = services;

    /// <summary>Gets the service collection <c>AddDucky</c> was called on.</summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Gets or sets the store's DI lifetime. Unset, it is <see cref="ServiceLifetime.Singleton"/> in the browser and
    /// <see cref="ServiceLifetime.Scoped"/> elsewhere, read after <c>configure</c> returns.
    /// <see cref="ServiceLifetime.Transient"/> is a configuration error (DUCKY301).
    /// </summary>
    public ServiceLifetime Lifetime
    {
        get => _lifetime ?? (IsBrowser ? ServiceLifetime.Singleton : ServiceLifetime.Scoped);
        set => _lifetime = value;
    }

    /// <summary>Gets or sets the causal depth beyond which an action is dropped. Defaults to 64.</summary>
    public int MaxDispatchDepth { get; set; } = Dispatcher.DefaultMaxDispatchDepth;

    /// <summary>Gets or sets a value indicating whether a dispatched action that no reducer and no async effect handles
    /// is a failure (<see cref="ReducerFailed"/> with an <see cref="UnhandledActionException"/>) rather than a Debug log.
    /// Defaults to false.</summary>
    public bool ThrowOnUnhandledAction { get; set; }

    /// <summary>Gets or sets the soft bound of the init buffer; overflow aborts init and drops nothing. Defaults to 1024.</summary>
    public int InitBufferCapacity { get; set; } = Dispatcher.DefaultInitBufferCapacity;

    /// <summary>Gets or sets how long init may take before the store becomes ready anyway. Defaults to 10 seconds.</summary>
    public TimeSpan InitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets how long disposal waits for the store to drain. Defaults to 2 seconds.</summary>
    public TimeSpan DisposeTimeout { get; set; } = TimeSpan.FromSeconds(2);

#pragma warning disable RS0030 // justification: the single IsBrowser read of this package (§5.1)
    internal bool IsBrowser { get; set; } = OperatingSystem.IsBrowser();
#pragma warning restore RS0030

    /// <summary>Registers a slice. A second call for the same type does nothing.</summary>
    /// <typeparam name="TSlice">The slice type.</typeparam>
    /// <returns>This builder.</returns>
    public DuckyBuilder AddSlice<TSlice>()
        where TSlice : Slice, new()
    {
        if (_sliceTypes.Add(typeof(TSlice)))
        {
            // A throwaway instance, read once. Whatever its constructor or Key throws never leaves AddDucky: DUCKY307/DUCKY308
            // join the other errors, any other exception is reported with them, under the slice's name (INV-31).
            try
            {
                var probe = new TSlice();
                _slices.Add(new(typeof(TSlice), probe.Key, probe.StateType, static () => new TSlice()));
            }
#pragma warning disable CA1031 // justification: a slice constructor is user code; its exception is reported at first resolution (INV-31)
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // new TSlice() runs through Activator.CreateInstance<T>, which wraps what the constructor throws; Key doesn't.
                var thrown = exception is TargetInvocationException ? exception.InnerException! : exception;
                if (thrown is DuckyConfigurationException configuration)
                {
                    _sliceErrors.AddRange(configuration.Errors);
                }
                else
                {
                    _sliceFailures.Add(($"Slice {DuckyErrors.Display(typeof(TSlice))}", thrown));
                }
            }
        }

        return this;
    }

    /// <summary>Registers a middleware. A second call for the same type does nothing: the first call fixes its position.</summary>
    /// <typeparam name="TMiddleware">The middleware type, created by the store from its scope.</typeparam>
    /// <returns>This builder.</returns>
    public DuckyBuilder Use<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TMiddleware : Middleware
    {
        if (_middlewareTypes.Add(typeof(TMiddleware)))
        {
            _middleware.Add(static services => Construct<TMiddleware>(services));
            _ctorChecks.Add(new(typeof(TMiddleware), "Use<T>"));
        }

        return this;
    }

    /// <summary>
    /// Registers an effect. The store creates it from its scope on its first use, owns it and disposes it; registering
    /// the type in DI as well changes nothing. A second call for the same type does nothing, and so does a call for a
    /// type registered with <see cref="AddEffect{TEffect}(TEffect)"/>: one runner per effect type.
    /// </summary>
    /// <typeparam name="TEffect">The effect type.</typeparam>
    /// <returns>This builder.</returns>
    public DuckyBuilder AddEffect<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TEffect>()
        where TEffect : Effect
    {
        _effects.TryAdd(typeof(TEffect), new(static services => Construct<TEffect>(services), new(typeof(TEffect), "AddEffect<T>")));
        return this;
    }

    /// <summary>
    /// Registers an effect instance, which replaces a registration of the same <typeparamref name="TEffect"/> whatever the
    /// call order, even when the instance derives from it (a test double). The store uses it but never disposes it.
    /// </summary>
    /// <typeparam name="TEffect">The effect type the registration is keyed by, not the instance's runtime type.</typeparam>
    /// <param name="instance">The effect.</param>
    /// <returns>This builder.</returns>
    public DuckyBuilder AddEffect<TEffect>(TEffect instance)
        where TEffect : Effect
    {
        ArgumentNullException.ThrowIfNull(instance);
        _effects[typeof(TEffect)] = new(_ => instance, CtorCheck: null);
        return this;
    }

    /// <summary>Adds a rule run with the core rules at the first store resolution, on the final configuration.</summary>
    /// <param name="rule">Returns the problems it finds; none when the configuration is valid.</param>
    /// <returns>This builder.</returns>
    public DuckyBuilder AddValidation(Func<IServiceProvider, IEnumerable<DuckyError>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    /// <summary>
    /// Queues <paramref name="type"/> for the DUCKY309 constructor check at the first store resolution, for a type the
    /// store doesn't construct itself (such as a reactive effect). The check passes when one of the constructors
    /// <c>ActivatorUtilities</c> would pick can resolve every parameter without a default value.
    /// </summary>
    /// <param name="type">The type whose constructor dependencies must resolve.</param>
    /// <param name="requiredBy">What needs the type, named in the error (for example <c>AddReactiveEffect&lt;T&gt;</c>).</param>
    /// <returns>This builder.</returns>
    public DuckyBuilder RequireResolvableConstructor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, string requiredBy)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(requiredBy);
        _ctorChecks.Add(new(type, requiredBy));
        return this;
    }

    // Every type AddSlice was given, a throwing one included: each is injectable, and resolving it reports the errors.
    internal IEnumerable<Type> SliceTypes => _sliceTypes;

    // Called once, when configure returned: the snapshot AddDucky registers.
    internal DuckyConfig Freeze() => new(Lifetime, IsBrowser, MaxDispatchDepth, ThrowOnUnhandledAction, InitBufferCapacity, InitTimeout, DisposeTimeout, [.. _slices], [.. _sliceErrors], [.. _sliceFailures], [.. _rules], [.. _middleware], [.. _effects.Values],
        [.. _effects.Values.Select(effect => effect.CtorCheck).OfType<CtorCheck.Requirement>().Concat(_ctorChecks).Distinct()]);

    // Runs at the store's first use, for an effect or a middleware. A throwing constructor becomes DUCKY353 wrapping what it
    // threw (§5.1), which the store caches and rethrows at every later use. That includes the InvalidOperationException a
    // constructor that uses the store gets from re-entering materialization (§6.6).
    private static T Construct<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IServiceProvider services)
        where T : class
    {
        try
        {
            return ActivatorUtilities.CreateInstance<T>(services);
        }
        catch (Exception exception)
        {
            throw new DuckyConfigurationException([DuckyErrors.ConstructorThrew(typeof(T), exception)], exception);
        }
    }
}
