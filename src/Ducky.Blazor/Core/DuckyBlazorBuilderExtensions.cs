using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>Registers Ducky.Blazor on a <see cref="DuckyBuilder"/>.</summary>
public static class DuckyBlazorBuilderExtensions
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

    // The last call for a slice wins, like the last value written to an option. DUCKY310 for a Prerender<T> slice never
    // added with AddSlice is M6-03's AddValidation rule (over Registration.Prerender's keys); until it lands, such a slice
    // is never seeded, silently.
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
        return registration;
    }
}

// What AddBlazor, Prerender<T> and (later) Persist<T> compose for one builder; its registration marks the first AddBlazor.
internal sealed record BlazorRegistration(BlazorOptions Options)
{
    /// <summary>The Prerender&lt;T&gt; slice types, each with its include predicate (null: always seeded).</summary>
    public Dictionary<Type, Func<object, bool>?> Prerender { get; } = [];
}
