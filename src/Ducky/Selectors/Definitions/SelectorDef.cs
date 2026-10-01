using System.Runtime.CompilerServices;

namespace Ducky;

/// <summary>A selector definition from <see cref="Selector"/>: immutable and holding no cache, so it is safe as a static field.</summary>
/// <typeparam name="T">The type of the projected result.</typeparam>
public sealed class SelectorDef<T>
{
    private readonly SelectorInput[] _inputs;
    private readonly Func<object?[], T> _project;

    internal SelectorDef(SelectorInput[] inputs, Func<object?[], T> project)
    {
        _inputs = inputs;
        _project = project;
    }

    /// <summary>Reads the inputs from <paramref name="state"/> and runs the projector, without any cache.</summary>
    /// <param name="state">The snapshot to read.</param>
    /// <returns>The projected result.</returns>
    public T Evaluate(StateSnapshot state) => _project(SelectorInput.ReadAll(_inputs, state));

    /// <summary>
    /// Creates a selector with a single-entry cache of its own: it reruns the projector only when an input changed since its
    /// last run, and otherwise returns the same result instance. No cache is shared between two calls.
    /// </summary>
    /// <returns>The memoized selector.</returns>
    public Func<StateSnapshot, T> Create()
    {
        // SPEC §6.9: one immutable entry per created selector, swapped whole. Two threads may both compute; the last write
        // wins, and both results are valid because projectors are pure.
        Entry? entry = null;
        return state =>
        {
            var values = SelectorInput.ReadAll(_inputs, state);
            var cached = Volatile.Read(ref entry);
            if (cached is not null && Matches(cached.Inputs, values))
            {
                return cached.Result;
            }

            var result = _project(values);
            Volatile.Write(ref entry, new Entry(values, result));
            return result;
        };
    }

    private bool Matches(object?[] cached, object?[] current)
    {
        for (var i = 0; i < _inputs.Length; i++)
        {
            if (!_inputs[i].Matches(cached[i], current[i]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record Entry(object?[] Inputs, T Result);
}

/// <summary>An argument selector definition from <see cref="Selector"/>: immutable and holding no cache.</summary>
/// <typeparam name="TArg">The type of the argument, such as an id.</typeparam>
/// <typeparam name="T">The type of the projected result.</typeparam>
public sealed class SelectorDef<TArg, T>
{
    private readonly SelectorInput[] _inputs;

    // Takes the inputs' values followed by the argument.
    private readonly Func<object?[], T> _project;

    internal SelectorDef(SelectorInput[] inputs, Func<object?[], T> project)
    {
        _inputs = inputs;
        _project = project;
    }

    /// <summary>Reads the inputs from <paramref name="state"/> and runs the projector with <paramref name="arg"/>, without any cache.</summary>
    /// <param name="state">The snapshot to read.</param>
    /// <param name="arg">The argument.</param>
    /// <returns>The projected result.</returns>
    public T Evaluate(StateSnapshot state, TArg arg) => _project([.. SelectorInput.ReadAll(_inputs, state), arg]);

    /// <summary>
    /// Creates a selector with a single-entry cache of its own, reading the argument from <paramref name="argument"/> on
    /// each call: it reruns the projector only when an input or the argument changed since its last run.
    /// </summary>
    /// <param name="argument">Reads the current argument, such as a component parameter.</param>
    /// <param name="argComparer">Compares arguments; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The memoized selector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null.</exception>
    public Func<StateSnapshot, T> Create(Func<TArg> argument, IEqualityComparer<TArg>? argComparer = null)
    {
        ArgumentNullException.ThrowIfNull(argument);
        var arg = SelectorInput.Of(_ => argument(), argComparer ?? EqualityComparer<TArg>.Default);
        return new SelectorDef<T>([.. _inputs, arg], _project).Create();
    }
}

// One selector input: how to read it, and when a cached value still matches (SPEC §6.9).
internal readonly record struct SelectorInput(Func<StateSnapshot, object?> Read, Func<object?, object?, bool> Matches)
{
    // A reference matches only the same instance (ADR-0007); a value type matches by EqualityComparer.Default.
    public static SelectorInput Of<TIn>(Func<StateSnapshot, TIn> read, [CallerArgumentExpression(nameof(read))] string? name = null)
    {
        ArgumentNullException.ThrowIfNull(read, name);
        return Of(read, typeof(TIn).IsValueType ? EqualityComparer<TIn>.Default : (IEqualityComparer<TIn>)(object)ReferenceEqualityComparer.Instance);
    }

    public static SelectorInput Of<TIn>(Func<StateSnapshot, TIn> read, IEqualityComparer<TIn> comparer) =>
        new(state => read(state), (cached, current) => comparer.Equals((TIn)cached!, (TIn)current!));

    public static object?[] ReadAll(SelectorInput[] inputs, StateSnapshot state) => Array.ConvertAll(inputs, input => input.Read(state));
}
