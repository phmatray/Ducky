namespace TheBlazorState.Attributes;

/// <summary>
/// Marks a partial property for state persistence across prerender-to-interactive transitions.
/// The source generator emits the property backing implementation, a Meta companion,
/// and lifecycle wiring.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class PersistAttribute : Attribute
{
    /// <summary>
    /// Optional time-to-live. If the persisted value is older than this duration,
    /// it is considered stale and the async factory (if any) will be re-invoked.
    /// Format: TimeSpan string (e.g., "00:05:00" for 5 minutes).
    /// </summary>
    public string? TimeToLive { get; set; }
}
