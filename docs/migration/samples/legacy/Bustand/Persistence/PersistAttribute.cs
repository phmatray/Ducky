namespace Bustand.Persistence;

/// <summary>
/// Marks a store for automatic state persistence to browser storage.
/// Apply to store classes that should persist state across page reloads.
/// </summary>
/// <remarks>
/// <para>
/// Storage key is auto-generated from the store type's full name (e.g., "MyApp.Stores.CounterStore")
/// unless a custom key is provided via the Key property.
/// </para>
/// <para>
/// A global prefix can be configured via BustandOptions.StorageKeyPrefix to namespace
/// all storage keys (e.g., "MyApp.MyApp.Stores.CounterStore").
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [BustandStore]
/// [Persist(StorageType.Local)]  // Auto key: "Bustand.MyNamespace.CounterStore"
/// public class CounterStore : ZustandStore&lt;CounterState&gt; { }
///
/// [BustandStore]
/// [Persist(StorageType.Session, Key = "counter")]  // Custom key: "Bustand.counter"
/// public class SessionCounterStore : ZustandStore&lt;CounterState&gt; { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PersistAttribute : Attribute
{
    /// <summary>
    /// Gets the storage type (Local or Session).
    /// </summary>
    public StorageType Storage { get; }

    /// <summary>
    /// Gets or sets an optional custom storage key.
    /// If not set, the store's full type name is used.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Creates a new persist attribute with the specified storage type.
    /// </summary>
    /// <param name="storage">The browser storage type to use.</param>
    public PersistAttribute(StorageType storage)
    {
        Storage = storage;
    }
}
