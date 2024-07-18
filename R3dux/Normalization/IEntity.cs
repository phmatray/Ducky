namespace R3dux.Normalization;

/// <summary>
/// Represents an entity with an identifier.
/// </summary>
/// <typeparam name="TKey">The type of the entity's key.</typeparam>
public interface IEntity<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// Gets the identifier of the entity.
    /// </summary>
    TKey Id { get; }
}