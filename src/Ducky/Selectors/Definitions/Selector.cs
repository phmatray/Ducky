namespace Ducky;

/// <summary>
/// Defines memoized selectors (SPEC §5.9). A definition holds no cache, so it is safe as a static field; each
/// <c>Create</c> call returns a selector with a cache of its own.
/// </summary>
/// <remarks>
/// Inputs read parts of a <see cref="StateSnapshot"/>; the projector combines them and must be pure. A created selector
/// reruns the projector only when an input changes: a reference by identity, a value type by
/// <see cref="EqualityComparer{T}.Default"/>.
/// </remarks>
public static class Selector
{
    /// <summary>Defines a selector over one input.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="projector">Projects the inputs to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<T> Define<T1, T>(Func<StateSnapshot, T1> in1, Func<T1, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new([SelectorInput.Of(in1)], v => projector((T1)v[0]!));
    }

    /// <summary>Defines a selector over two inputs.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="projector">Projects the inputs to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<T> Define<T1, T2, T>(Func<StateSnapshot, T1> in1, Func<StateSnapshot, T2> in2, Func<T1, T2, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new([SelectorInput.Of(in1), SelectorInput.Of(in2)], v => projector((T1)v[0]!, (T2)v[1]!));
    }

    /// <summary>Defines a selector over three inputs.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="T3">The type of the third input.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="in3">Reads the third input from a snapshot.</param>
    /// <param name="projector">Projects the inputs to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<T> Define<T1, T2, T3, T>(
        Func<StateSnapshot, T1> in1, Func<StateSnapshot, T2> in2, Func<StateSnapshot, T3> in3, Func<T1, T2, T3, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new(
            [SelectorInput.Of(in1), SelectorInput.Of(in2), SelectorInput.Of(in3)],
            v => projector((T1)v[0]!, (T2)v[1]!, (T3)v[2]!));
    }

    /// <summary>Defines a selector over four inputs.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="T3">The type of the third input.</typeparam>
    /// <typeparam name="T4">The type of the fourth input.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="in3">Reads the third input from a snapshot.</param>
    /// <param name="in4">Reads the fourth input from a snapshot.</param>
    /// <param name="projector">Projects the inputs to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<T> Define<T1, T2, T3, T4, T>(
        Func<StateSnapshot, T1> in1,
        Func<StateSnapshot, T2> in2,
        Func<StateSnapshot, T3> in3,
        Func<StateSnapshot, T4> in4,
        Func<T1, T2, T3, T4, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new(
            [SelectorInput.Of(in1), SelectorInput.Of(in2), SelectorInput.Of(in3), SelectorInput.Of(in4)],
            v => projector((T1)v[0]!, (T2)v[1]!, (T3)v[2]!, (T4)v[3]!));
    }

    /// <summary>Defines a selector over one input and an argument, such as an id.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="projector">Projects the inputs and the argument to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<TArg, T> Define<T1, TArg, T>(Func<StateSnapshot, T1> in1, Func<T1, TArg, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new([SelectorInput.Of(in1)], v => projector((T1)v[0]!, (TArg)v[1]!));
    }

    /// <summary>Defines a selector over two inputs and an argument, such as an id.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="projector">Projects the inputs and the argument to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<TArg, T> Define<T1, T2, TArg, T>(
        Func<StateSnapshot, T1> in1, Func<StateSnapshot, T2> in2, Func<T1, T2, TArg, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new([SelectorInput.Of(in1), SelectorInput.Of(in2)], v => projector((T1)v[0]!, (T2)v[1]!, (TArg)v[2]!));
    }

    /// <summary>Defines a selector over three inputs and an argument, such as an id.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="T3">The type of the third input.</typeparam>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="in3">Reads the third input from a snapshot.</param>
    /// <param name="projector">Projects the inputs and the argument to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<TArg, T> Define<T1, T2, T3, TArg, T>(
        Func<StateSnapshot, T1> in1, Func<StateSnapshot, T2> in2, Func<StateSnapshot, T3> in3, Func<T1, T2, T3, TArg, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new(
            [SelectorInput.Of(in1), SelectorInput.Of(in2), SelectorInput.Of(in3)],
            v => projector((T1)v[0]!, (T2)v[1]!, (T3)v[2]!, (TArg)v[3]!));
    }

    /// <summary>Defines a selector over four inputs and an argument, such as an id.</summary>
    /// <typeparam name="T1">The type of the first input.</typeparam>
    /// <typeparam name="T2">The type of the second input.</typeparam>
    /// <typeparam name="T3">The type of the third input.</typeparam>
    /// <typeparam name="T4">The type of the fourth input.</typeparam>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="T">The type of the projected result.</typeparam>
    /// <param name="in1">Reads the first input from a snapshot.</param>
    /// <param name="in2">Reads the second input from a snapshot.</param>
    /// <param name="in3">Reads the third input from a snapshot.</param>
    /// <param name="in4">Reads the fourth input from a snapshot.</param>
    /// <param name="projector">Projects the inputs and the argument to the result; must be pure.</param>
    /// <returns>An immutable definition holding no cache.</returns>
    /// <exception cref="ArgumentNullException">A delegate is null.</exception>
    public static SelectorDef<TArg, T> Define<T1, T2, T3, T4, TArg, T>(
        Func<StateSnapshot, T1> in1,
        Func<StateSnapshot, T2> in2,
        Func<StateSnapshot, T3> in3,
        Func<StateSnapshot, T4> in4,
        Func<T1, T2, T3, T4, TArg, T> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new(
            [SelectorInput.Of(in1), SelectorInput.Of(in2), SelectorInput.Of(in3), SelectorInput.Of(in4)],
            v => projector((T1)v[0]!, (T2)v[1]!, (T3)v[2]!, (T4)v[3]!, (TArg)v[4]!));
    }
}
