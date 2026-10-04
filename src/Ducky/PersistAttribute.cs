namespace Ducky;

/// <summary>
/// Persists a slice's state through Ducky.Blazor. The generator turns it into a builder call (GEN-02); the runtime never
/// reads it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PersistAttribute : Attribute
{
    /// <summary>Gets the version of the persisted shape; a stored value of another version is migrated or discarded.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Gets where the state is stored.</summary>
    public PersistStorage Storage { get; init; } = PersistStorage.Local;

    /// <summary>Gets a value indicating whether changes in one browser tab are applied in the others.</summary>
    public bool SyncAcrossTabs { get; init; }
}
