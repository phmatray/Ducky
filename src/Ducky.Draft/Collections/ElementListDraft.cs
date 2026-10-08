using System.Collections;
using System.Collections.Immutable;

namespace Ducky.Draft;

/// <summary>
/// A draft of an <see cref="ImmutableList{T}"/> or <see cref="ImmutableArray{T}"/> whose elements are
/// <see cref="DraftableAttribute">[Draftable]</see> records: reading an element returns its element draft, created lazily on
/// first access and cached by index. A structural operation (insert, remove, clear) first flushes the cached element drafts
/// into the list, revokes them and clears the cache, so index shifts stay trivially correct; a later access re-drafts from the
/// flushed list. Appending shifts no index and keeps the cache. Its <c>Build*</c> methods flush the element drafts and return
/// the source itself when nothing changed and it is of the requested kind. Revoking this draft revokes every cached element
/// draft. A null element (possible only for an oblivious <typeparamref name="T"/>) is never drafted: it reads as null and
/// stays null.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TDraft">The element's draft type.</typeparam>
public sealed class ListDraft<T, TDraft> : IReadOnlyList<TDraft>, IRevocable
    where T : class
    where TDraft : class, IDraft<T>
{
    private readonly ListDraft<T> _items;
    private readonly Func<T, TDraft> _createDraft;
    private readonly Dictionary<int, TDraft> _drafts = [];
    private int _version; // bumped by each structural operation: a running enumeration then throws

    /// <summary>Creates a draft of <paramref name="source"/>; also how a caller replaces the whole list.</summary>
    /// <param name="source">The source list.</param>
    /// <param name="createDraft">Creates the draft of an element.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="createDraft"/> is null.</exception>
    public ListDraft(ImmutableList<T> source, Func<T, TDraft> createDraft)
        : this((ListDraft<T>)(source ?? throw new ArgumentNullException(nameof(source))), createDraft)
    {
    }

    /// <summary>Creates a draft of <paramref name="source"/>; a default array reads as empty.</summary>
    /// <param name="source">The source array.</param>
    /// <param name="createDraft">Creates the draft of an element.</param>
    /// <exception cref="ArgumentNullException"><paramref name="createDraft"/> is null.</exception>
    public ListDraft(ImmutableArray<T> source, Func<T, TDraft> createDraft)
        : this((ListDraft<T>)source, createDraft)
    {
    }

    private ListDraft(ListDraft<T> items, Func<T, TDraft> createDraft)
    {
        _items = items;
        _createDraft = createDraft ?? throw new ArgumentNullException(nameof(createDraft));
    }

    /// <summary>Gets a value indicating whether the draft may differ from its source.</summary>
    public bool IsDirty => _items.IsDirty || _drafts.Values.Any(draft => draft.IsDirty);

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <summary>Gets the lazy draft of the element at <paramref name="index"/>, or null for a null element.</summary>
    /// <param name="index">The index.</param>
    public TDraft this[int index] => Drafts.GetOrCreate(_drafts, index, _items[index], _createDraft);

    /// <summary>Adds <paramref name="item"/> at the end; the cached element drafts stay live.</summary>
    /// <param name="item">The record to add.</param>
    public void Add(T item) => _items.Add(item);

    /// <summary>Inserts <paramref name="item"/> at <paramref name="index"/> (structural).</summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The record to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range; nothing is flushed or revoked.</exception>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        FlushAndRevoke();
        _items.Insert(index, item);
    }

    /// <summary>Replaces the element at <paramref name="index"/>, revoking its cached draft.</summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The new record.</param>
    public void Set(int index, T item)
    {
        _items[index] = item;
        Drafts.Revoke(_drafts, index);
    }

    /// <summary>Removes the element at <paramref name="index"/> (structural).</summary>
    /// <param name="index">The index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range; nothing is flushed or revoked.</exception>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        FlushAndRevoke();
        _items.RemoveAt(index);
    }

    /// <summary>Removes every element whose draft matches <paramref name="match"/> (structural when one matches).</summary>
    /// <param name="match">The predicate, called with each element's draft (null for a null element).</param>
    /// <returns>The number of elements removed.</returns>
    public int RemoveAll(Predicate<TDraft> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var hits = Enumerable.Range(0, Count).Where(index => match(this[index])).ToList();
        if (hits.Count > 0)
        {
            FlushAndRevoke();
            hits.Reverse(); // remove from the end so the earlier indices hold
            hits.ForEach(_items.RemoveAt);
        }

        return hits.Count;
    }

    /// <summary>Removes every element (structural).</summary>
    public void Clear()
    {
        FlushAndRevoke();
        _items.Clear();
    }

    /// <summary>Builds an <see cref="ImmutableList{T}"/>: the source itself when nothing changed and it is a list.</summary>
    /// <returns>The built list.</returns>
    public ImmutableList<T> BuildList()
    {
        Flush();
        return _items.BuildList();
    }

    /// <summary>Builds an <see cref="ImmutableArray{T}"/>: the source itself (default included) when nothing changed and it is an array.</summary>
    /// <returns>The built array.</returns>
    public ImmutableArray<T> BuildArray()
    {
        Flush();
        return _items.BuildArray();
    }

    /// <summary>
    /// Walks the element drafts by index, creating each on first visit. A structural operation (insert, remove, clear) during the
    /// walk makes the next step throw <see cref="InvalidOperationException"/>, as <see cref="List{T}"/> does; edits through the
    /// visited drafts, <see cref="Set"/> and <see cref="Add"/> (no index shift) are fine.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<TDraft> GetEnumerator()
    {
        var count = Count; // a revoked draft throws here, not at the first MoveNext
        var version = _version;
        return Enumerable.Range(0, count).Select(index => version == _version ? this[index] : throw Drafts.Modified()).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    void IRevocable.Revoke()
    {
        Drafts.RevokeAll(_drafts);
        ((IRevocable)_items).Revoke();
    }

    // Writes each cached draft's result into the list by reference: an unchanged element (Build returns it) is not a change, and
    // an edit survives an element Equals that ignores the edited member (INV-26).
    private void Flush()
    {
        foreach (var (index, draft) in _drafts)
        {
            var built = draft.Build();
            if (!ReferenceEquals(built, _items[index]))
            {
                _items.Replace(index, built);
            }
        }
    }

    private void FlushAndRevoke()
    {
        Flush();
        Drafts.RevokeAll(_drafts);
        _version++;
    }
}
