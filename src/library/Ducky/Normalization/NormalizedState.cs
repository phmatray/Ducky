// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Represents a normalized state for collections.
/// </summary>
/// <typeparam name="TKey">The type of the entity key.</typeparam>
/// <typeparam name="TEntity">The type of the entity value.</typeparam>
/// <typeparam name="TState">The type of the state.</typeparam>
public abstract record NormalizedState<TKey, TEntity, TState>
    : INormalizedStateCollectionMethods<TKey, TEntity, TState>
    where TKey : IEquatable<TKey>
    where TEntity : IEntity<TKey>
    where TState : NormalizedState<TKey, TEntity, TState>, new()
{
    /// <summary>
    /// Gets or init the dictionary of entities.
    /// </summary>
    public ImmutableDictionary<TKey, TEntity> ById { get; init; } = ImmutableDictionary<TKey, TEntity>.Empty;

    /// <summary>
    /// Gets the collection of entity IDs.
    /// </summary>
    public ValueCollection<TKey> AllIds
        => [..ById.Keys];

    /// <summary>
    /// Indexer to get an entity by its key.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns>The entity associated with the specified key.</returns>
    public TEntity this[TKey key]
        => GetByKey(key);

    /// <summary>
    /// Creates a new state with the specified entities.
    /// </summary>
    /// <param name="entities">The entities to create the state with.</param>
    /// <returns>A new state with the entities.</returns>
    public static TState Create(in ValueCollection<TEntity> entities)
    {
        return new() { ById = entities.ToImmutableDictionary(entity => entity.Id) };
    }

    /// <summary>
    /// Selects entities.
    /// </summary>
    /// <returns>A value collection of entities.</returns>
    public ValueCollection<TEntity> SelectEntities()
    {
        return [..ById.Values];
    }

    /// <summary>
    /// Selects entities based on a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities.</param>
    /// <returns>A collection of entities that match the predicate.</returns>
    public ValueCollection<TEntity> SelectEntities(Func<TEntity, bool> predicate)
    {
        return [..ById.Values.Where(predicate)];
    }

    /// <inheritdoc />
    public TState AddOne(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return CreateWith(ById.Add(entity.Id, entity));
    }

    /// <inheritdoc />
    public TState AddMany(params IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return CreateWith(ById.AddRange(entities.ToImmutableDictionary(entity => entity.Id)));
    }

    /// <inheritdoc />
    public TState SetAll(params IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return CreateWith(entities.ToImmutableDictionary(entity => entity.Id));
    }

    /// <inheritdoc />
    public TState SetOne(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return CreateWith(ById.SetItem(entity.Id, entity));
    }

    /// <inheritdoc />
    public TState SetMany(params IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return CreateWith(ById.SetItems(entities.ToImmutableDictionary(entity => entity.Id)));
    }

    /// <inheritdoc />
    public TState RemoveOne(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return (key is string)
            ? throw new DuckyException("The key cannot be empty.")
            : CreateWith(ById.Remove(key));
    }

    /// <inheritdoc />
    public TState RemoveMany(params IEnumerable<TKey> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return CreateWith(ById.RemoveRange(keys));
    }

    /// <inheritdoc />
    public TState RemoveMany(Func<TEntity, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return CreateWith(ById.RemoveRange(ById.Values.Where(predicate).Select(entity => entity.Id)));
    }

    /// <inheritdoc />
    public TState RemoveAll()
    {
        return CreateWith(ImmutableDictionary<TKey, TEntity>.Empty);
    }

    /// <inheritdoc />
    public TState UpdateOne(TKey key, Action<TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(update);

        TEntity entity = GetByKey(key);
        update(entity);
        return CreateWith(ById.SetItem(key, entity));
    }

    /// <inheritdoc />
    public TState UpdateOne(TKey key, Func<TEntity, TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(update);

        TEntity entity = GetByKey(key);
        entity = update(entity);
        return CreateWith(ById.SetItem(key, entity));
    }

    /// <inheritdoc />
    public TState UpdateMany(IEnumerable<TKey> keys, Action<TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(update);

        ImmutableDictionary<TKey, TEntity> byId = ById;
        foreach (TKey key in keys)
        {
            TEntity entity = GetByKey(key);
            update(entity);
            byId = byId.SetItem(key, entity);
        }

        return CreateWith(byId);
    }

    /// <inheritdoc />
    public TState UpdateMany(IEnumerable<TKey> keys, Func<TEntity, TEntity> update)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(update);

        ImmutableDictionary<TKey, TEntity> byId = ById;
        foreach (TKey key in keys)
        {
            TEntity entity = GetByKey(key);
            entity = update(entity);
            byId = byId.SetItem(key, entity);
        }

        return CreateWith(byId);
    }

    /// <inheritdoc />
    public TState UpsertOne(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return CreateWith(ById.SetItem(entity.Id, entity));
    }

    /// <inheritdoc />
    public TState UpsertMany(params IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return CreateWith(ById.AddRange(entities.ToImmutableDictionary(entity => entity.Id)));
    }

    /// <inheritdoc />
    public TState MapOne(TKey key, Func<TEntity, TEntity> map)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(map);
        return CreateWith(ById.SetItem(key, map(GetByKey(key))));
    }

    /// <inheritdoc />
    public TState Map(Func<TEntity, TEntity> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return CreateWith(ById.ToImmutableDictionary(kvp => kvp.Key, kvp => map(kvp.Value)));
    }

    /// <summary>
    /// Checks if an entity with the specified key exists in the state.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns><c>true</c> if the entity exists; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return (key is string)
            ? throw new DuckyException("The key cannot be empty.")
            : ById.ContainsKey(key);
    }

    /// <summary>
    /// Gets an entity by its key.
    /// </summary>
    /// <param name="key">The key of the entity.</param>
    /// <returns>The entity if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="DuckyException">The entity does not exist.</exception>
    public TEntity GetByKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return (ById.TryGetValue(key, out TEntity? value))
            ? value
            : throw new DuckyException("The entity does not exist.");
    }

    /// <summary>
    /// Merges the specified entities into the state using the provided merge strategy.
    /// </summary>
    /// <param name="entities">The entities to merge into the state.</param>
    /// <param name="strategy">The strategy to use when merging entities.</param>
    /// <returns>A new state with the entities merged.</returns>
    /// <exception cref="DuckyException">The state must be of type TState.</exception>
    public TState Merge(
        ImmutableDictionary<TKey, TEntity> entities,
        MergeStrategy strategy = MergeStrategy.FailIfDuplicate)
    {
        ArgumentNullException.ThrowIfNull(entities);

        ImmutableDictionary<TKey, TEntity> byId = strategy switch
        {
            MergeStrategy.FailIfDuplicate => MergeFailIfDuplicate(entities),
            MergeStrategy.Overwrite => ById.SetItems(entities),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null)
        };

        return CreateWith(byId);
    }

    private ImmutableDictionary<TKey, TEntity> MergeFailIfDuplicate(ImmutableDictionary<TKey, TEntity> entities)
    {
        foreach (KeyValuePair<TKey, TEntity> kvp in entities.Where(kvp => ById.ContainsKey(kvp.Key)))
        {
            throw new DuckyException($"Duplicate entity with key '{kvp.Key}' found during merge.");
        }

        return ById.AddRange(entities);
    }

    private TState CreateWith(ImmutableDictionary<TKey, TEntity> byId)
    {
        return this with { ById = byId } as TState
            ?? throw new DuckyException("The state must be of type TState.");
    }
}
