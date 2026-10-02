using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ducky;

// The builder as AddDucky left it: later builder calls change nothing. Validation runs once per container, at its first
// store resolution, and an invalid configuration rethrows that same exception at every later resolution in that
// container (§8.1, INV-31). The result lives in ValidationState, never here: every provider built from the collection
// shares this instance through the descriptor.
internal sealed class DuckyConfig(
    ServiceLifetime lifetime,
    bool isBrowser,
    int maxDispatchDepth,
    TimeSpan initTimeout,
    TimeSpan disposeTimeout,
    DuckyConfig.SliceRegistration[] slices,
    DuckyError[] sliceErrors,
    (string Source, Exception Thrown)[] sliceFailures,
    Func<IServiceProvider, IEnumerable<DuckyError>>[] rules,
    Func<IServiceProvider, Middleware>[] middleware,
    DuckyConfig.EffectRegistration[] effects,
    CtorCheck.Requirement[] ctorChecks)
{
    public ServiceLifetime Lifetime => lifetime;

    public bool IsBrowser => isBrowser;

    public int MaxDispatchDepth => maxDispatchDepth;

    public TimeSpan InitTimeout => initTimeout;

    public TimeSpan DisposeTimeout => disposeTimeout;

    public IEnumerable<Slice> CreateSlices() => slices.Select(slice => slice.Create());

    // In registration order, from the store's services (§6.10).
    public Middleware[] CreateMiddleware(IServiceProvider services) => Array.ConvertAll(middleware, create => create(services));

    // In registration order, from the store's services: one per effect type (§5.1).
    public (Effect Effect, bool Owned)[] CreateEffects(IServiceProvider services) =>
        Array.ConvertAll(effects, effect => (effect.Create(services), effect.Owned));

    public void ThrowIfInvalid(IServiceProvider services, ILogger logger)
    {
        var failure = services.GetRequiredService<ValidationState>().Ensure(() => Validate(services, new SafeLogger(logger)));
        if (failure is not null)
        {
            throw failure;
        }
    }

    private DuckyConfigurationException? Validate(IServiceProvider services, SafeLogger logger)
    {
        List<DuckyError> errors = [.. sliceErrors];
        List<(string Source, Exception Thrown)> failures = [.. sliceFailures];
        if (lifetime == ServiceLifetime.Transient)
        {
            errors.Add(DuckyErrors.TransientLifetime());
        }

        Dictionary<string, Type> keys = [];
        Dictionary<Type, Type> stateTypes = [];
        foreach (var (type, key, stateType, _) in slices)
        {
            // A generic slice that kept the derived key: FromType can't name its closed types apart.
            if (type.IsGenericType && key == SliceKey.FromType(type))
            {
                errors.Add(DuckyErrors.GenericSliceWithoutKey(type));
            }
            else if (key is null || !SliceKey.IsValid(key, type.Assembly))
            {
                errors.Add(DuckyErrors.InvalidKey(type, key!));
            }
            else if (!keys.TryAdd(key, type))
            {
                errors.Add(DuckyErrors.DuplicateKey(key, keys[key], type));
            }

            if (!stateTypes.TryAdd(stateType, type))
            {
                errors.Add(DuckyErrors.DuplicateStateType(stateType, stateTypes[stateType], type));
            }
        }

        errors.AddRange(CtorCheck.Run(ctorChecks, services, logger));
        for (var i = 0; i < rules.Length; i++)
        {
            // A throwing rule keeps what it returned before the throw and hides none of the other errors.
            try
            {
                errors.AddRange(rules[i](services));
            }
#pragma warning disable CA1031 // justification: a rule is user code; its exception is reported with the other errors (INV-31)
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failures.Add(($"AddValidation rule #{i + 1}", exception));
            }
        }

        return errors.Count == 0 && failures.Count == 0 ? null : new DuckyConfigurationException(errors, failures);
    }

    // A slice read once through its throwaway instance by AddSlice; Create makes each store's own instance.
    internal readonly record struct SliceRegistration(Type Type, string Key, Type StateType, Func<Slice> Create);

    // An AddEffect<T> registration is created by the store, which checks its constructor (DUCKY309), owns and disposes
    // it; an AddEffect(instance) instance has no CtorCheck and is never disposed.
    internal readonly record struct EffectRegistration(Func<IServiceProvider, Effect> Create, CtorCheck.Requirement? CtorCheck)
    {
        public bool Owned => CtorCheck is not null;
    }

    // Registered by AddDucky through a factory, so each container creates its own (an instance registration would be
    // shared by every provider built from the collection, which is the bug this type exists to avoid).
    internal sealed class ValidationState
    {
        private DuckyConfigurationException? _failure;
        private bool _validated;
        private bool _validating; // read and written only under _lock
        private object? _lock;

        public DuckyConfigurationException? Ensure(Func<DuckyConfigurationException?> validate) =>
            LazyInitializer.EnsureInitialized(ref _failure, ref _validated, ref _lock, () =>
            {
                // The lock is reentrant: a rule that resolves the store re-enters here on the same thread. Without this
                // flag the recursion never ends; with it, the rule's catch records the throw as a failure of the report.
                if (_validating)
                {
                    throw new InvalidOperationException(
                        "An AddValidation rule resolved the store (or a service that depends on it) while the store was being validated. "
                        + "Resolve only the services the rule checks, never IStore, IDispatcher, a slice or anything built from them.");
                }

                _validating = true;
                try
                {
                    return validate();
                }
                finally
                {
                    _validating = false;
                }
            });
    }
}
