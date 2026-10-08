using System.Collections;
using System.Collections.Immutable;

namespace Ducky.Draft;

/// <summary>
/// A lazy copy-on-write draft of an <see cref="ImmutableList{T}"/> or <see cref="ImmutableArray{T}"/>: it reads its source
/// until the first change. Its <c>Build*</c> methods return that source itself when nothing changed and it is of the
/// requested kind, else a new instance of that kind. An edit that leaves the contents unchanged (an equal set, removing a
/// missing item, clearing an empty draft, adding no items) is not a change; <see cref="Sort"/> always is. Enumeration walks a
/// snapshot, so editing the draft inside a <c>foreach</c> over it never throws.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public sealed class ListDraft<T> : IList<T>, IReadOnlyList<T>, IRevocable
{
    private readonly ImmutableArray<T> _array;
    private readonly IList<T> _source;
    private ImmutableList<T>.Builder? _builder;
    private bool _revoked;

    private ListDraft(ImmutableList<T> list) => _source = list;

    private ListDraft(ImmutableArray<T> array)
    {
        _array = array;
        _source = array.IsDefault ? ImmutableArray<T>.Empty : array; // a default array reads as empty
    }

    /// <summary>Gets a value indicating whether the draft was changed.</summary>
    public bool IsDirty
    {
        get
        {
            Guard();
            return _builder is not null;
        }
    }

    /// <inheritdoc />
    public int Count => Read.Count;

    /// <inheritdoc />
    bool ICollection<T>.IsReadOnly
    {
        get
        {
            Guard();
            return false;
        }
    }

    private IList<T> Read
    {
        get
        {
            Guard();
            return (IList<T>?)_builder ?? _source;
        }
    }

    private IList<T> ReadAt(int index)
    {
        var read = Read;
        if ((uint)index >= (uint)read.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return read;
    }

    private ImmutableList<T>.Builder Write
    {
        get
        {
            Guard();
            return _builder ??= ImmutableList.CreateRange(_source).ToBuilder(); // CreateRange returns a list source as-is
        }
    }

    /// <inheritdoc cref="IList{T}.this[int]" />
    public T this[int index]
    {
        get => ReadAt(index)[index];
        set
        {
            if (!EqualityComparer<T>.Default.Equals(ReadAt(index)[index], value))
            {
                Write[index] = value;
            }
        }
    }

    /// <summary>Creates a draft of <paramref name="list"/>.</summary>
    /// <param name="list">The source list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="list"/> is null.</exception>
    public static implicit operator ListDraft<T>(ImmutableList<T> list) => new(list ?? throw new ArgumentNullException(nameof(list)));

    /// <summary>Creates a draft of <paramref name="array"/>; a default array reads as empty.</summary>
    /// <param name="array">The source array.</param>
    public static implicit operator ListDraft<T>(ImmutableArray<T> array) => new(array);

    /// <summary>Builds an <see cref="ImmutableList{T}"/>: the source itself when clean and a list.</summary>
    /// <returns>The built list.</returns>
    public ImmutableList<T> BuildList()
    {
        Guard();
        return _builder?.ToImmutable() ?? ImmutableList.CreateRange(_source);
    }

    /// <summary>Builds an <see cref="ImmutableArray{T}"/>: the source itself (default included) when clean and an array.</summary>
    /// <returns>The built array.</returns>
    public ImmutableArray<T> BuildArray() => _source is ImmutableList<T> || IsDirty ? [.. Read] : _array;

    /// <inheritdoc />
    public int IndexOf(T item) => Read.IndexOf(item);

    /// <inheritdoc />
    public bool Contains(T item) => Read.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => Read.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator()
    {
        var read = Read;
        return (read is ImmutableList<T>.Builder builder ? builder.ToImmutable() : read).GetEnumerator(); // the builder caches it
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Add(T item) => Write.Add(item);

    /// <inheritdoc />
    public void Insert(int index, T item) => Write.Insert(index, item);

    /// <inheritdoc />
    public bool Remove(T item)
    {
        var index = Read.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        Write.RemoveAt(index);
        return true;
    }

    /// <inheritdoc />
    public void RemoveAt(int index) => Write.RemoveAt(index);

    /// <inheritdoc />
    public void Clear()
    {
        if (Read.Count > 0)
        {
            Write.Clear();
        }
    }

    /// <summary>Adds <paramref name="items"/> at the end.</summary>
    /// <param name="items">The items to add.</param>
    public void AddRange(IEnumerable<T> items)
    {
        Guard();
        ImmutableArray<T> added = [.. items]; // throws ArgumentNullException on null; materialized: safe when items is this draft
        if (!added.IsEmpty)
        {
            Write.AddRange(added);
        }
    }

    /// <summary>Removes every element matching <paramref name="match"/>.</summary>
    /// <param name="match">The predicate.</param>
    /// <returns>The number of elements removed.</returns>
    public int RemoveAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        return Read.Any(match.Invoke) ? Write.RemoveAll(match) : 0;
    }

    /// <summary>Sorts the elements with <paramref name="comparison"/>.</summary>
    /// <param name="comparison">The comparison.</param>
    public void Sort(Comparison<T> comparison) => Write.Sort(comparison);

    /// <summary>
    /// Writes <paramref name="item"/> at <paramref name="index"/> even when it equals the current element, so an element draft's
    /// result flushed by reference survives an <c>Equals</c> that ignores the edited member (INV-26).
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="item">The item.</param>
    internal void Replace(int index, T item) => Write[index] = item;

    /// <inheritdoc />
    void IRevocable.Revoke() => _revoked = true;

    private void Guard() => Drafts.ThrowIfRevoked(_revoked, nameof(ListDraft<T>));
}
