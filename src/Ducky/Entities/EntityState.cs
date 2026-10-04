using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ducky;

/// <summary>
/// An immutable, insertion-ordered collection of entities with unique ids (SPEC §5.10, ADR-0044). Every operation
/// returns a new instance, or this instance when nothing changes (INV-32, ADR-0007); only <see cref="Items"/> is
/// serialized.
/// </summary>
/// <typeparam name="TKey">The id type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed class EntityState<TKey, TEntity>
    where TKey : notnull
    where TEntity : class, IEntity<TKey>
{
    // Built on first use and cached per instance (a benign race: two threads build equal indexes). The public
    // constructor builds it at once, since checking for duplicate ids is the same pass.
    private FrozenDictionary<TKey, int>? _index;

    /// <summary>Initializes a new instance of the <see cref="EntityState{TKey, TEntity}"/> class.</summary>
    /// <param name="items">The entities, in insertion order.</param>
    /// <exception cref="ArgumentException">Two entities share an id.</exception>
    [JsonConstructor]
    public EntityState(ImmutableList<TEntity> items)
        : this(items, BuildIndex(items))
    {
    }

    private EntityState(ImmutableList<TEntity> items, FrozenDictionary<TKey, int>? index)
    {
        Items = items;
        _index = index;
    }

    /// <summary>Gets the empty state.</summary>
#pragma warning disable CA1000 // justification: Empty's name and place are the public API (SPEC §5.10)
    public static EntityState<TKey, TEntity> Empty { get; } = new([]);
#pragma warning restore CA1000

    /// <summary>Gets the entities in insertion order; the only serialized member.</summary>
    public ImmutableList<TEntity> Items { get; }

    /// <summary>Gets the number of entities.</summary>
    [JsonIgnore]
    public int Count => Items.Count;

    private FrozenDictionary<TKey, int> Index => _index ??= BuildIndex(Items);

    /// <summary>Gets the entity with the given id, or null.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The entity, or null when no entity has that id.</returns>
    public TEntity? Find(TKey id) => Index.TryGetValue(id, out var position) ? Items[position] : null;

    /// <summary>Gets whether an entity has the given id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>True when an entity has that id.</returns>
    public bool Contains(TKey id) => Index.ContainsKey(id);

    /// <summary>Replaces every entity.</summary>
    /// <param name="entities">The new entities, in order.</param>
    /// <returns>The new state, or this one when <paramref name="entities"/> holds the same instances in the same order.</returns>
    /// <exception cref="ArgumentException">Two entities share an id.</exception>
    public EntityState<TKey, TEntity> SetAll(IEnumerable<TEntity> entities)
    {
        var items = entities.ToImmutableList();
        return items.SequenceEqual(Items, ReferenceEqualityComparer.Instance) ? this : new(items);
    }

    /// <summary>Appends entities.</summary>
    /// <param name="entities">The entities to append.</param>
    /// <returns>The new state, or this one when <paramref name="entities"/> is empty.</returns>
    /// <exception cref="ArgumentException">An id is already stored or repeated in <paramref name="entities"/>.</exception>
    public EntityState<TKey, TEntity> Add(params ReadOnlySpan<TEntity> entities) => MergeCore(entities, MergeStrategy.FailIfDuplicate);

    /// <summary>Replaces each stored entity in place and appends the others.</summary>
    /// <param name="entities">The entities; a repeated id ends with its last entity.</param>
    /// <returns>The new state, or this one when every entity is the instance already stored.</returns>
    public EntityState<TKey, TEntity> Upsert(params ReadOnlySpan<TEntity> entities) => MergeCore(entities, MergeStrategy.PreferIncoming);

    /// <summary>Replaces one entity with the result of <paramref name="update"/>.</summary>
    /// <param name="id">The id.</param>
    /// <param name="update">Returns the new entity, or the same instance for no change; it must keep the id.</param>
    /// <returns>The new state, or this one when the id is missing or <paramref name="update"/> returns the same instance.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="update"/> changed the id.</exception>
    public EntityState<TKey, TEntity> Update(TKey id, Func<TEntity, TEntity> update)
    {
        if (!Index.TryGetValue(id, out var position))
        {
            return this;
        }

        var current = Items[position];
        var next = update(current);
        return ReferenceEquals(next, current) ? this : new(Items.SetItem(position, KeepId(current, next)), null);
    }

    /// <summary>Replaces every entity with the result of <paramref name="map"/>.</summary>
    /// <param name="map">Returns the new entity, or the same instance for no change; it must keep the id.</param>
    /// <returns>The new state, or this one when <paramref name="map"/> returns every entity unchanged.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="map"/> changed an id.</exception>
    public EntityState<TKey, TEntity> Map(Func<TEntity, TEntity> map)
    {
        ImmutableList<TEntity>.Builder? items = null;
        var position = 0;
        foreach (var current in Items)
        {
            var next = map(current);
            if (!ReferenceEquals(next, current))
            {
                items ??= Items.ToBuilder();
                items[position] = KeepId(current, next);
            }

            position++;
        }

        return items is null ? this : new(items.ToImmutable(), null);
    }

    /// <summary>Removes the entities with the given ids; a missing id is ignored.</summary>
    /// <param name="ids">The ids.</param>
    /// <returns>The new state, or this one when no id is stored.</returns>
    public EntityState<TKey, TEntity> Remove(params ReadOnlySpan<TKey> ids)
    {
        HashSet<TKey>? removed = null;
        foreach (var id in ids)
        {
            if (Contains(id))
            {
                (removed ??= []).Add(id);
            }
        }

        return removed is null ? this : new(Items.RemoveAll(entity => removed.Contains(entity.Id)), null);
    }

    /// <summary>Removes the entities that match <paramref name="predicate"/>.</summary>
    /// <param name="predicate">The removal condition.</param>
    /// <returns>The new state, or this one when no entity matches.</returns>
    public EntityState<TKey, TEntity> RemoveWhere(Func<TEntity, bool> predicate)
    {
        var items = Items.RemoveAll(entity => predicate(entity));
        return items.Count == Items.Count ? this : new(items, null);
    }

    /// <summary>Appends the entities whose id is new and resolves the others by <paramref name="strategy"/>.</summary>
    /// <param name="entities">The entities, applied in order.</param>
    /// <param name="strategy">What to do with an id already stored or repeated in <paramref name="entities"/>.</param>
    /// <returns>The new state, or this one when nothing changes.</returns>
    /// <exception cref="ArgumentException"><see cref="MergeStrategy.FailIfDuplicate"/> and a duplicate id.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="strategy"/> is not a defined value.</exception>
    public EntityState<TKey, TEntity> Merge(IEnumerable<TEntity> entities, MergeStrategy strategy)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)strategy, (uint)MergeStrategy.PreferIncoming, nameof(strategy));
        return MergeCore([.. entities], strategy);
    }

    private static FrozenDictionary<TKey, int> BuildIndex(ImmutableList<TEntity> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var index = new Dictionary<TKey, int>(items.Count);
        foreach (var entity in items)
        {
            if (!index.TryAdd(entity.Id, index.Count))
            {
                throw Duplicate(entity.Id);
            }
        }

        return index.ToFrozenDictionary();
    }

    private static ArgumentException Duplicate(TKey id) => new($"An entity with id '{id}' is already present.");

    private static TEntity KeepId(TEntity current, TEntity next) =>
        EqualityComparer<TKey>.Default.Equals(current.Id, next.Id)
            ? next
            : throw new InvalidOperationException($"The update changed the id of entity '{current.Id}' to '{next.Id}'.");

    private EntityState<TKey, TEntity> MergeCore(ReadOnlySpan<TEntity> entities, MergeStrategy strategy)
    {
        ImmutableList<TEntity>.Builder? items = null;
        Dictionary<TKey, int>? appended = null;
        foreach (var entity in entities)
        {
            if (!Index.TryGetValue(entity.Id, out var position) && appended?.TryGetValue(entity.Id, out position) != true)
            {
                items ??= Items.ToBuilder();
                (appended ??= []).Add(entity.Id, items.Count);
                items.Add(entity);
            }
            else if (strategy == MergeStrategy.FailIfDuplicate)
            {
                throw Duplicate(entity.Id);
            }
            else if (strategy == MergeStrategy.PreferIncoming && !ReferenceEquals(items is null ? Items[position] : items[position], entity))
            {
                items ??= Items.ToBuilder();
                items[position] = entity;
            }
        }

        // A later entity can restore the stored instance an earlier one replaced (INV-32): the O(n) re-check runs only
        // when an in-place replacement happened.
        return items is null || (appended is null && items.SequenceEqual(Items, ReferenceEqualityComparer.Instance))
            ? this
            : new(items.ToImmutable(), null);
    }
}
