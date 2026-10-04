namespace Ducky;

/// <summary>An entity of an <see cref="EntityState{TKey, TEntity}"/>, identified by its <see cref="Id"/> (SPEC §5.10).</summary>
/// <typeparam name="TKey">The id type.</typeparam>
public interface IEntity<out TKey>
    where TKey : notnull
{
    /// <summary>Gets the entity's id, unique within an <see cref="EntityState{TKey, TEntity}"/>.</summary>
    TKey Id { get; }
}
