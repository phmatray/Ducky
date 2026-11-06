# EntityState

`EntityState<TKey, TEntity>` is an immutable collection of entities with unique ids. It keeps insertion order and looks entities up by id without scanning. Entities implement `IEntity<TKey>`.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EntityStateGuideTests.cs#entity-state)]

Every operation returns a new collection, or the same instance when nothing changed:

| Operation | Returns the same instance when |
|---|---|
| `Upsert(entities)` | every entity is the stored one (same reference) |
| `Update(id, update)` | the id is missing, or `update` returned the same entity |
| `Map(map)` | no entity changed |
| `Remove(ids)` | nothing was removed |

`Add` throws `ArgumentException` for an id that exists, and so does the constructor for duplicate ids. `Merge(entities, strategy)` combines a batch with the stored entities: `PreferIncoming` replaces the stored ones, `PreferExisting` keeps them, `FailIfDuplicate` throws. `SetAll` replaces everything, `RemoveWhere` removes by predicate.

Keeping the instance matters because the store compares references: `Todos.With` above returns the same `Todos` when the collection didn't change, so toggling a missing id commits nothing and notifies nobody.

## Read

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/EntityStateGuideTests.cs#entity-read)]

Only `Items` is serialized: `Count` and the id index are rebuilt, never stored.
