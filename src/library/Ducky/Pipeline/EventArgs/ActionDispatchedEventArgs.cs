namespace Ducky.Pipeline;

/// <summary>
/// Event args for actions dispatched through <see cref="Dispatcher"/>.
/// </summary>
public class ActionDispatchedEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDispatchedEventArgs"/> class.
    /// </summary>
    /// <param name="action">The dispatched action.</param>
    public ActionDispatchedEventArgs(object action)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// The action that was dispatched.
    /// </summary>
    public object Action { get; }
}
