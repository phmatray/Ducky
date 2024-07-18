using System.Collections.Immutable;
using R3dux.Exceptions;

namespace R3dux.Normalization;

/// <summary>
/// Represents a normalized state for collections.
/// </summary>
/// <typeparam name="TKey">The type of the entity key.</typeparam>
/// <typeparam name="TEntity">The type of the entity value.</typeparam>
/// <typeparam name="TState">The type of the state.</typeparam>
public abstract record NormalizedState<TKey, TEntity, TState>
    where TKey : notnull
    where TEntity : IEntity<TKey>
    where TState : NormalizedState<TKey, TEntity, TState>, new()
{
    /// <summary>
    /// Gets or sets the dictionary of entities.
    /// </summary>
    public ImmutableDictionary<TKey, TEntity> ById { get; init; } = ImmutableDictionary<TKey, TEntity>.Empty;
    
    /// <summary>
    /// Gets the list of entity IDs.
    /// </summary>
    public ImmutableList<TKey> AllIds
        => ById.Keys.ToImmutableList();
    
    /// <summary>
    /// Indexer to get an entity by its key.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns>The entity associated with the specified key.</returns>
    public TEntity this[TKey key]
        => GetByKey(key);
    
    /// <summary>
    /// Selects entities.
    /// </summary>
    /// <returns>An immutable list of entities.</returns>
    public ImmutableList<TEntity> SelectImmutableList()
        => ById.Values.ToImmutableList();
    
    /// <summary>
    /// Selects entities based on a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>An immutable list of entities that match the predicate.</returns>
    public ImmutableList<TEntity> SelectImmutableList(Func<TEntity, bool> predicate)
        => ById.Values.Where(predicate).ToImmutableList();

    /// <summary>
    /// Adds or updates an entity in the state.
    /// </summary>
    /// <param name="entity">The entity to add or update.</param>
    /// <returns>A new state with the entity added or updated.</returns>
    /// <exception cref="R3duxException">The state must be of type TState.</exception>
    public TState AddOrUpdate(TEntity entity)
        => this with { ById = ById.SetItem(entity.Id, entity) } as TState
           ?? throw new R3duxException("The state must be of type TState.");

    /// <summary>
    /// Removes an entity from the state.
    /// </summary>
    /// <param name="key">The key of the entity to remove.</param>
    /// <returns>A new state with the entity removed.</returns>
    /// <exception cref="R3duxException">The state must be of type TState.</exception>
    public TState Remove(TKey key)
        => this with { ById = ById.Remove(key) } as TState
           ?? throw new R3duxException("The state must be of type TState.");

    /// <summary>
    /// Checks if an entity with the specified key exists in the state.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns><c>true</c> if the entity exists; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key)
        => ById.ContainsKey(key);

    /// <summary>
    /// Gets an entity by its key.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns>The entity if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="R3duxException">The entity does not exist.</exception>
    public TEntity GetByKey(TKey key)
        => ById.TryGetValue(key, out var value)
            ? value
            : throw new R3duxException("The entity does not exist.");
    
    /// <summary>
    /// Creates a new state with the specified entities.
    /// </summary>
    /// <param name="entities">The entities to create the state with.</param>
    /// <returns>A new state with the entities.</returns>
    /// <exception cref="R3duxException">The state must be of type TState.</exception>
    public static TState Create(ImmutableList<TEntity> entities)
    {
        var byId = entities.ToImmutableDictionary(entity => entity.Id);
        var state = new TState { ById = byId };
        return state ?? throw new R3duxException("The state must be of type TState.");
    }
}