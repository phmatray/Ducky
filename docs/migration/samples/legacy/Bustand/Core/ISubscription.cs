namespace Bustand.Core;

/// <summary>
/// Represents an active subscription to store state changes.
/// Dispose to unsubscribe and stop receiving updates.
/// </summary>
/// <example>
/// <code>
/// var subscription = store.Subscribe(s => s.Count, () => Console.WriteLine("Count changed!"));
/// // Later, when done:
/// subscription.Dispose();
/// </code>
/// </example>
public interface ISubscription : IDisposable
{
    /// <summary>
    /// Gets whether this subscription is still active.
    /// </summary>
    /// <remarks>
    /// Returns <c>false</c> after <see cref="IDisposable.Dispose"/> or <see cref="Unsubscribe"/> has been called.
    /// </remarks>
    bool IsActive { get; }

    /// <summary>
    /// Unsubscribes from state changes and stops receiving updates.
    /// This is functionally equivalent to <see cref="IDisposable.Dispose"/> but may be more
    /// semantically clear in component code.
    /// </summary>
    /// <remarks>
    /// After calling this method, <see cref="IsActive"/> will return <c>false</c>.
    /// Multiple calls are safe and have no effect after the first call.
    /// </remarks>
    void Unsubscribe();
}
