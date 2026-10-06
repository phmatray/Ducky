using System.ComponentModel;

namespace Ducky.Blazor;

public static partial class DuckyBlazorBuilderExtensions
{
    /// <summary>
    /// Persists <typeparamref name="TSlice"/> to storage and hydrates it at init (SPEC §11.5). Adds Ducky.Blazor, and
    /// requires the state's JSON type info from the <c>UseJson</c> resolver (DUCKY306). Configuration composes: every
    /// <paramref name="configure"/> for the slice is applied, in call order, to one <see cref="PersistOptions"/>, checked at
    /// the first store resolution.
    /// </summary>
    /// <typeparam name="TSlice">The slice, added with <see cref="DuckyBuilder.AddSlice{TSlice}"/> (DUCKY310).</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Sets the slice's options; <see langword="null"/> keeps them.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder Persist<TSlice>(this DuckyBuilder builder, Action<PersistOptions>? configure = null)
        where TSlice : Slice, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        var layers = Persisted(builder, typeof(TSlice));
        if (configure is not null)
        {
            layers.Configure.Add(configure);
        }

        return builder;
    }

    /// <summary>
    /// Generated code only (<c>[Persist]</c>): persists <typeparamref name="TSlice"/> like
    /// <see cref="Persist{TSlice}"/>, with the attribute's values applied before every <see cref="Persist{TSlice}"/>
    /// delegate whatever the call order, so the builder wins.
    /// </summary>
    /// <typeparam name="TSlice">The slice.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Sets the attribute's explicitly written values.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static DuckyBuilder PersistAttributeDefaults<TSlice>(this DuckyBuilder builder, Action<PersistOptions> configure)
        where TSlice : Slice, new()
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        Persisted(builder, typeof(TSlice)).Defaults.Add(configure);
        return builder;
    }

    private static PersistLayers Persisted(DuckyBuilder builder, Type slice)
    {
        var persist = Registration(builder).Persist;
        if (!persist.TryGetValue(slice, out var layers))
        {
            persist.Add(slice, layers = new());
            builder.RequireJsonTypeInfo(StateType(slice), $"Persist<{slice.Name}>");
            builder.AddValidation(_ => BlazorRegistration.ValidatePersist(builder, slice, layers));
        }

        return layers;
    }
}

// One persisted slice's delegates: the attribute's base layer, then the builder's, each in call order. Composed once, on
// first read of Options: never before configure returned, since the first read is the validation rule at first
// resolution. Per slice, so a throwing delegate fails only its own slice.
internal sealed class PersistLayers
{
    private readonly Lazy<PersistOptions> _options;

    public PersistLayers() => _options = new(Compose);

    public List<Action<PersistOptions>> Defaults { get; } = [];

    public List<Action<PersistOptions>> Configure { get; } = [];

    public PersistOptions Options => _options.Value;

    private PersistOptions Compose()
    {
        var options = new PersistOptions();
        foreach (var configure in Defaults.Concat(Configure))
        {
            configure(options);
        }

        return options;
    }
}
