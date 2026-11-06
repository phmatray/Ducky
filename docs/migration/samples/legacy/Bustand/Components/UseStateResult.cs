namespace Bustand.Components;

/// <summary>
/// Result of UseState() call. Provides current value access.
/// </summary>
/// <typeparam name="T">The type of the state slice.</typeparam>
public readonly struct UseStateResult<T>
{
    private readonly Func<T> _getter;

    internal UseStateResult(Func<T> getter)
    {
        _getter = getter;
    }

    /// <summary>
    /// Gets the current value of the state slice.
    /// </summary>
    public T Value => _getter();

    /// <summary>
    /// Implicit conversion to T for ergonomic usage.
    /// </summary>
    public static implicit operator T(UseStateResult<T> result) => result.Value;

    /// <summary>
    /// Returns the string representation of the current value.
    /// </summary>
    public override string? ToString() => Value?.ToString();
}
