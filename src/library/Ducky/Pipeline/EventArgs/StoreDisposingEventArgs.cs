namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when the store begins disposal.
/// </summary>
public sealed class StoreDisposingEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreDisposingEventArgs"/> class.
    /// </summary>
    /// <param name="uptime">The uptime of the store before disposal.</param>
    public StoreDisposingEventArgs(in TimeSpan uptime)
    {
        Uptime = uptime;
    }

    /// <summary>
    /// Gets the uptime of the store before disposal.
    /// </summary>
    public TimeSpan Uptime { get; }
}
