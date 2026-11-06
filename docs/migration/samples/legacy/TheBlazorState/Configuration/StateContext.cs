using TheBlazorState.Storage;

namespace TheBlazorState.Configuration;

/// <summary>
/// Configuration context passed to ConfigureState(StateContext ctx).
/// Provides per-property configurators (generated as properties) and component-level settings.
/// This is a base class; the generator creates a nested subclass with typed property configurators.
/// </summary>
public class StateContext
{
    /// <summary>
    /// Component-level storage strategy override.
    /// Applied to all [Persist] properties that don't have their own storage set.
    /// </summary>
    public IStorageStrategy? Storage { get; set; }
}
