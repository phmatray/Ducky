namespace Ducky;

/// <summary>
/// A selected value: <see cref="Value"/> evaluates the selector against the store's current snapshot on every read.
/// Dispose it to stop its change callback.
/// </summary>
/// <typeparam name="T">The selected value's type.</typeparam>
public sealed class Selection<T> : IDisposable
{
    private readonly Func<T> _read;
    private readonly Action<T>? _onRead;
    private Action? _onDispose;

    private Selection(Func<T> read, Action<T>? onRead, Action? onDispose)
    {
        _read = read;
        _onRead = onRead;
        _onDispose = onDispose;
    }

    /// <summary>Gets the value, evaluated on each read.</summary>
    public T Value
    {
        get
        {
            var value = _read();
            _onRead?.Invoke(value);
            return value;
        }
    }

    /// <summary>
    /// Creates a selection for an extension package: <see cref="Value"/> calls <paramref name="read"/>, then
    /// <paramref name="onRead"/> with the value; <see cref="Dispose"/> calls <paramref name="onDispose"/> once.
    /// </summary>
    /// <param name="read">Evaluates the value.</param>
    /// <param name="onRead">Called with each value read, after <paramref name="read"/>.</param>
    /// <param name="onDispose">Called by the first <see cref="Dispose"/>.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="read"/> is null.</exception>
#pragma warning disable CA1000 // justification: the factory's name and place are the public API (SPEC §5.2)
    public static Selection<T> Create(Func<T> read, Action<T>? onRead = null, Action? onDispose = null)
#pragma warning restore CA1000
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(read, onRead, onDispose);
    }

    /// <summary>Reads <paramref name="selection"/>'s <see cref="Value"/>.</summary>
    /// <param name="selection">The selection to read.</param>
    public static implicit operator T(Selection<T> selection) => selection.Value;

    /// <summary>Returns <see cref="Value"/>'s text, so a selection renders as its value in Razor.</summary>
    /// <returns>The value's <see cref="object.ToString"/>, or null when the value is null.</returns>
    public override string? ToString() => Value?.ToString();

    /// <summary>
    /// Unsubscribes the change callback; idempotent. Never waits for a callback already running.
    /// </summary>
    public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
}
