namespace R3dux.Normalization;

public interface INormalizedStateCollectionMethods<in TKey, TEntity, out TState>
    where TKey : notnull
    where TEntity : IEntity<TKey>
    where TState : NormalizedState<TKey, TEntity, TState>, new()
{
    /// <summary>
    /// Add one entity to the collection.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    TState AddOne(TEntity entity);
    
    /// <summary>
    /// Add multiple entities to the collection.
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    TState AddMany(IEnumerable<TEntity> entities);
    
    /// <summary>
    /// Replace current collection with provided collection.
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    TState SetAll(IEnumerable<TEntity> entities);
    
    /// <summary>
    /// Add or Replace one entity in the collection.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    TState SetOne(TEntity entity);
    
    /// <summary>
    /// Add or Replace multiple entities in the collection.
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    TState SetMany(IEnumerable<TEntity> entities);
    
    /// <summary>
    /// Remove one entity from the collection.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    TState RemoveOne(TKey key);
    
    /// <summary>
    /// Remove multiple entities from the collection by id.
    /// </summary>
    /// <param name="keys"></param>
    /// <returns></returns>
    TState RemoveMany(IEnumerable<TKey> keys);
    
    /// <summary>
    /// Remove multiple entities from the collection by predicate.
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    TState RemoveMany(Func<TEntity, bool> predicate);
    
    /// <summary>
    /// Clear entity collection.
    /// </summary>
    /// <returns></returns>
    TState RemoveAll();
    
    /// <summary>
    /// Update one entity in the collection. Supports partial updates.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="update"></param>
    /// <returns></returns>
    TState UpdateOne(TKey key, Action<TEntity> update);
    
    /// <summary>
    /// Update multiple entities in the collection. Supports partial updates.
    /// </summary>
    /// <param name="keys"></param>
    /// <param name="update"></param>
    /// <returns></returns>
    TState UpdateMany(IEnumerable<TKey> keys, Action<TEntity> update);
    
    /// <summary>
    /// Add or Update one entity in the collection.
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    TState UpsertOne(TEntity entity);
    
    /// <summary>
    /// Add or Update multiple entities in the collection.
    /// </summary>
    /// <param name="entities"></param>
    /// <returns></returns>
    TState UpsertMany(IEnumerable<TEntity> entities);
    
    /// <summary>
    /// Update one entity in the collection by defining a map function.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="map"></param>
    /// <returns></returns>
    TState MapOne(TKey key, Func<TEntity, TEntity> map);
    
    /// <summary>
    /// Update multiple entities in the collection by defining a map function
    /// </summary>
    /// <param name="map"></param>
    /// <returns></returns>
    TState Map(Func<TEntity, TEntity> map);
}