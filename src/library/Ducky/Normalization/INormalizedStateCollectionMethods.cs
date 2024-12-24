// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Defines methods for managing a collection of entities within a normalized state.
/// </summary>
/// <typeparam name="TKey">The type of the entity key.</typeparam>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TState">The type of the state.</typeparam>
public interface INormalizedStateCollectionMethods<in TKey, TEntity, out TState>
    where TKey : IEquatable<TKey>
    where TEntity : IEntity<TKey>
    where TState : NormalizedState<TKey, TEntity, TState>, new()
{
    /// <summary>
    /// Adds one entity to the collection.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The new state with the entity added.</returns>
    /// <exception cref="ArgumentNullException">The entity must not be null.</exception>
    TState AddOne(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the collection.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <returns>The new state with the entities added.</returns>
    /// <exception cref="ArgumentNullException">The entities collection must not be null.</exception>
    TState AddMany(params IEnumerable<TEntity> entities);

    /// <summary>
    /// Replaces the current collection with the provided collection.
    /// </summary>
    /// <param name="entities">The entities to set.</param>
    /// <returns>The new state with the entities set.</returns>
    /// <exception cref="ArgumentNullException">The entities collection must not be null.</exception>
    TState SetAll(params IEnumerable<TEntity> entities);

    /// <summary>
    /// Adds or replaces one entity in the collection.
    /// </summary>
    /// <param name="entity">The entity to set.</param>
    /// <returns>The new state with the entity set.</returns>
    /// <exception cref="ArgumentNullException">The entity must not be null.</exception>
    TState SetOne(TEntity entity);

    /// <summary>
    /// Adds or replaces multiple entities in the collection.
    /// </summary>
    /// <param name="entities">The entities to set.</param>
    /// <returns>The new state with the entities set.</returns>
    /// <exception cref="ArgumentNullException">The entities collection must not be null.</exception>
    TState SetMany(params IEnumerable<TEntity> entities);

    /// <summary>
    /// Removes one entity from the collection.
    /// </summary>
    /// <param name="key">The key of the entity to remove.</param>
    /// <returns>The new state with the entity removed.</returns>
    /// <exception cref="ArgumentNullException">The key must not be null.</exception>
    TState RemoveOne(TKey key);

    /// <summary>
    /// Removes multiple entities from the collection by id.
    /// </summary>
    /// <param name="keys">The keys of the entities to remove.</param>
    /// <returns>The new state with the entities removed.</returns>
    /// <exception cref="ArgumentNullException">The keys collection must not be null.</exception>
    TState RemoveMany(params IEnumerable<TKey> keys);

    /// <summary>
    /// Removes multiple entities from the collection by a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter entities to remove.</param>
    /// <returns>The new state with the entities removed.</returns>
    /// <exception cref="ArgumentNullException">The predicate must not be null.</exception>
    TState RemoveMany(Func<TEntity, bool> predicate);

    /// <summary>
    /// Clears the entity collection.
    /// </summary>
    /// <returns>The new state with the collection cleared.</returns>
    TState RemoveAll();

    /// <summary>
    /// Updates one entity in the collection. Supports partial updates.
    /// </summary>
    /// <param name="key">The key of the entity to update.</param>
    /// <param name="update">The update action to apply to the entity.</param>
    /// <returns>The new state with the entity updated.</returns>
    /// <exception cref="ArgumentNullException">The key and update action must not be null.</exception>
    TState UpdateOne(TKey key, Action<TEntity> update);

    /// <summary>
    /// Updates one entity in the collection. Supports partial updates.
    /// </summary>
    /// <param name="key">The key of the entity to update.</param>
    /// <param name="update">The update function to apply to the entity.</param>
    /// <returns>The new state with the entity updated.</returns>
    /// <exception cref="ArgumentNullException">The key and update action must not be null.</exception>
    TState UpdateOne(TKey key, Func<TEntity, TEntity> update);

    /// <summary>
    /// Updates multiple entities in the collection. Supports partial updates.
    /// </summary>
    /// <param name="keys">The keys of the entities to update.</param>
    /// <param name="update">The update action to apply to the entities.</param>
    /// <returns>The new state with the entities updated.</returns>
    /// <exception cref="ArgumentNullException">The keys collection and update action must not be null.</exception>
    TState UpdateMany(IEnumerable<TKey> keys, Action<TEntity> update);

    /// <summary>
    /// Updates multiple entities in the collection. Supports partial updates.
    /// </summary>
    /// <param name="keys">The keys of the entities to update.</param>
    /// <param name="update">The update action to apply to the entities.</param>
    /// <returns>The new state with the entities updated.</returns>
    /// <exception cref="ArgumentNullException">The keys collection and update action must not be null.</exception>
    TState UpdateMany(IEnumerable<TKey> keys, Func<TEntity, TEntity> update);

    /// <summary>
    /// Adds or updates one entity in the collection.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>The new state with the entity upserted.</returns>
    /// <exception cref="ArgumentNullException">The entity must not be null.</exception>
    TState UpsertOne(TEntity entity);

    /// <summary>
    /// Adds or updates multiple entities in the collection.
    /// </summary>
    /// <param name="entities">The entities to upsert.</param>
    /// <returns>The new state with the entities upserted.</returns>
    /// <exception cref="ArgumentNullException">The entities collection must not be null.</exception>
    TState UpsertMany(params IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates one entity in the collection by defining a map function.
    /// </summary>
    /// <param name="key">The key of the entity to map.</param>
    /// <param name="map">The map function to apply to the entity.</param>
    /// <returns>The new state with the entity mapped.</returns>
    /// <exception cref="ArgumentNullException">The key and map function must not be null.</exception>
    TState MapOne(TKey key, Func<TEntity, TEntity> map);

    /// <summary>
    /// Updates multiple entities in the collection by defining a map function.
    /// </summary>
    /// <param name="map">The map function to apply to the entities.</param>
    /// <returns>The new state with the entities mapped.</returns>
    /// <exception cref="ArgumentNullException">The map function must not be null.</exception>
    TState Map(Func<TEntity, TEntity> map);
}
