namespace Ducky;

/// <summary>What <see cref="EntityState{TKey, TEntity}.Merge"/> does with an incoming entity whose id is already stored.</summary>
public enum MergeStrategy
{
    /// <summary>Throw <see cref="System.ArgumentException"/>, as <see cref="EntityState{TKey, TEntity}.Add"/> does.</summary>
    FailIfDuplicate,

    /// <summary>Keep the stored entity and ignore the incoming one.</summary>
    PreferExisting,

    /// <summary>Replace the stored entity in place, as <see cref="EntityState{TKey, TEntity}.Upsert"/> does.</summary>
    PreferIncoming,
}
