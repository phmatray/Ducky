namespace Bustand.Persistence;

/// <summary>
/// Specifies the browser storage type for state persistence.
/// </summary>
public enum StorageType
{
    /// <summary>
    /// Use localStorage - data persists until explicitly cleared.
    /// Survives browser restarts and tab closures.
    /// </summary>
    Local,

    /// <summary>
    /// Use sessionStorage - data cleared when tab/window closes.
    /// Survives page refreshes but not tab closure.
    /// </summary>
    Session
}
