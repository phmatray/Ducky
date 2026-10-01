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

    /// <summary>Gets or sets a value indicating whether an action no slice handles is a failure. Defaults to false.</summary>
    public bool ThrowOnUnhandledAction { get; set; }

    /// <summary>Gets or sets the soft bound of the init buffer; overflow aborts init and drops nothing. Defaults to 1024.</summary>
    public int InitBufferCapacity { get; set; } = 1024;

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

    /// <summary>Adds a rule run with the core rules at the first store resolution, on the final configuration.</summary>
    /// <param name="rule">Returns the problems it finds; none when the configuration is valid.</param>
    /// <returns>This builder.</returns>
    public DuckyBuilder AddValidation(Func<IServiceProvider, IEnumerable<DuckyError>> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    // Every type AddSlice was given, a throwing one included: each is injectable, and resolving it reports the errors.
    internal IEnumerable<Type> SliceTypes => _sliceTypes;

    // Called once, when configure returned: the snapshot AddDucky registers.
    internal DuckyConfig Freeze() => new(Lifetime, IsBrowser, MaxDispatchDepth, DisposeTimeout, [.. _slices], [.. _sliceErrors], [.. _sliceFailures], [.. _rules]);
}
