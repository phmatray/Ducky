using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>Registers Ducky.Blazor on a <see cref="DuckyBuilder"/>.</summary>
public static partial class DuckyBlazorBuilderExtensions
{
    /// <summary>
    /// Adds Ducky.Blazor to the store. Registration is idempotent: the first call registers the prerender handoff and then
    /// the persistence middleware, at fixed positions. Configuration composes: every <paramref name="configure"/> of every
    /// call is applied, in call order, to one <see cref="BlazorOptions"/>, so the last value written to a property wins.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Sets options; <see langword="null"/> only registers.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder AddBlazor(this DuckyBuilder builder, Action<BlazorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = Registration(builder).Options; // never inside the ?. below: a null configure still registers
        configure?.Invoke(options);
        return builder;
    }

    /// <summary>
    /// Seeds <typeparamref name="TSlice"/> from the prerender pass, or from a paused circuit, into the interactive store,
    /// so the interactive side starts from the prerendered state and its load rule (load unless
    /// <see cref="StateSnapshot.WasRestored(string)"/>) does not load it again (SPEC §11.4). Adds Ducky.Blazor, and
    /// requires the state's JSON type info from the <c>UseJson</c> resolver (DUCKY306).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WebAssembly copy of the seed is not data-protected: it is in the page HTML, like the rendered markup, so the
    /// slice must hold nothing the rendered page couldn't show.
    /// </para>
    /// <para>
    /// The seed is taken from a quiescent store, and quiescence excludes <see cref="Effect.LongRunning"/> effects and all
    /// Ducky.Reactive work: load the slice with an <see cref="Effect{TAction}"/>. A load that can fail during prerender (a
    /// server-side <c>HttpClient</c> calling a relative URL) would seed its error state, which the interactive side would
    /// never retry: register such a slice with <see cref="Prerender{TSlice, TState}"/> and an <c>include</c> that
    /// excludes error and incomplete states.
    /// </para>
    /// <para>
    /// A slice that would push the seed past <see cref="BlazorOptions.PrerenderSeedMaxWireBytes"/> is left out, and the
    /// interactive side loads it again.
    /// </para>
    /// </remarks>
    /// <typeparam name="TSlice">The slice, added with <see cref="DuckyBuilder.AddSlice{TSlice}"/>.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder Prerender<TSlice>(this DuckyBuilder builder)
        where TSlice : Slice, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        return Prerender(builder, typeof(TSlice), StateType(typeof(TSlice)), include: null);
    }

    /// <summary>
    /// Seeds <typeparamref name="TSlice"/> like <see cref="Prerender{TSlice}"/>, only while <paramref name="include"/>
    /// holds for its state when the seed is taken, so an error or incomplete state is left out and loads again on the
    /// interactive side.
    /// </summary>
    /// <remarks>The remarks of <see cref="Prerender{TSlice}"/> apply.</remarks>
    /// <typeparam name="TSlice">The slice, added with <see cref="DuckyBuilder.AddSlice{TSlice}"/>.</typeparam>
    /// <typeparam name="TState">The slice's state type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="include">Whether a state is seeded; it must be pure.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="include"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder Prerender<TSlice, TState>(this DuckyBuilder builder, Func<TState, bool> include)
        where TSlice : Slice<TState>, new()
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(include);
        return Prerender(builder, typeof(TSlice), typeof(TState), state => include((TState)state));
    }

    // The last call for a slice wins, like the last value written to an option. A slice never added with AddSlice is
    // DUCKY310, reported by the registration's validation rule.
    private static DuckyBuilder Prerender(DuckyBuilder builder, Type slice, Type state, Func<object, bool>? include)
    {
        Registration(builder).Prerender[slice] = include;
        return builder.RequireJsonTypeInfo(state, $"Prerender<{slice.Name}>");
    }

    // The declared state type, read from the slice's Slice<TState> base: a probe instance would run user code here, outside
    // AddSlice's error collection.
    private static Type StateType(Type slice)
    {
        while (!slice.IsConstructedGenericType || slice.GetGenericTypeDefinition() != typeof(Slice<>))
        {
            slice = slice.BaseType!;
        }

        return slice.GenericTypeArguments[0];
    }

    private static BlazorRegistration Registration(DuckyBuilder builder)
    {
        // The first call is told by a marker only AddBlazor registers: a BlazorOptions the app registered does not count.
        if (builder.Services.FirstOrDefault(static d => d.ServiceType == typeof(BlazorRegistration))?.ImplementationInstance is BlazorRegistration registration)
        {
            return registration;
        }

        registration = new(new BlazorOptions());
        builder.Services.AddSingleton(registration).AddSingleton(registration.Options);
        builder.Use<PrerenderHandoff>().Use<PersistenceMiddleware>();
        builder.AddSlice<PersistenceSlice>();

        // At first resolution, on the final composed options: a later call may still fix what an earlier one set (§11.1).
        builder.AddValidation(_ => registration.Validate(builder));
        return registration;
    }
}

// What AddBlazor, Prerender<T> and Persist<T> compose for one builder; its registration marks the first AddBlazor.
internal sealed class BlazorRegistration(BlazorOptions options)
{
    public BlazorOptions Options { get; } = options;

    /// <summary>The Prerender&lt;T&gt; slice types, each with its include predicate (null: always seeded).</summary>
    public Dictionary<Type, Func<object, bool>?> Prerender { get; } = [];

    /// <summary>The persisted slice types in first-call order, each with its configure delegates and composed options.</summary>
    public OrderedDictionary<Type, PersistLayers> Persist { get; } = [];

    // DUCKY310 per Prerender<T> slice, then DUCKY316. Each persisted slice has its own rule, ValidatePersist.
    public IEnumerable<DuckyError> Validate(DuckyBuilder builder)
    {
        foreach (var slice in Prerender.Keys.Where(slice => !Added(builder, slice)))
        {
            yield return BlazorErrors.SliceNotAdded("Prerender", slice);
        }

        if (Bound(Options.HydrationTimeout) >= Bound(builder.InitTimeout))
        {
            yield return BlazorErrors.HydrationTimeoutNotBelowInitTimeout(Options.HydrationTimeout, builder.InitTimeout);
        }

        if (Bound(Options.PrerenderSeedWaitTimeout) >= Bound(Options.HydrationTimeout))
        {
            yield return BlazorErrors.SeedWaitNotBelowHydrationTimeout(Options.PrerenderSeedWaitTimeout, Options.HydrationTimeout);
        }

        // Timeout.InfiniteTimeSpan (-1 ms) never fires, so it is the longest timeout, not the shortest.
        static TimeSpan Bound(TimeSpan timeout) => timeout == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : timeout;
    }

    // DUCKY310-312 for one persisted slice: its own AddValidation rule, so a throwing configure delegate fails only this
    // rule, after its DUCKY310, and the core reports that failure, named after the slice, with every other error and every
    // other slice's failure (INV-31).
    public static IEnumerable<DuckyError> ValidatePersist(DuckyBuilder builder, Type slice, PersistLayers layers)
    {
        if (!Added(builder, slice))
        {
            yield return BlazorErrors.SliceNotAdded("Persist", slice);
        }

        PersistOptions options;
        try
        {
            options = layers.Options;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Persist<{BlazorErrors.Display(slice)}> configure threw.", exception);
        }

        List<int> missing = [];
        for (var from = 1; from < options.Version; from++)
        {
            if (!options.Migrations.ContainsKey(from))
            {
                missing.Add(from);
            }
        }

        if (missing.Count > 0)
        {
            yield return BlazorErrors.MigrationGap(slice, options.Version, missing);
        }

        if (options.SyncAcrossTabs && options.Storage != PersistStorage.Local)
        {
            yield return BlazorErrors.SyncAcrossTabsWithoutLocal(slice, options.Storage);
        }
    }

    // A slice is added when AddDucky registered it for injection, which it does for every AddSlice<T>.
    private static bool Added(DuckyBuilder builder, Type slice) => builder.Services.Any(descriptor => descriptor.ServiceType == slice);
}
