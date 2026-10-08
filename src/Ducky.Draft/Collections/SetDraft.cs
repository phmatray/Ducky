using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Ducky.Draft;

/// <summary>
/// A lazy copy-on-write draft of an <see cref="ImmutableHashSet{T}"/> or <see cref="ImmutableSortedSet{T}"/>: it reads its
/// source until the first change, and every edit goes through the immutable set itself, so the source's comparer is kept.
/// Its <c>Build*</c> methods return that source itself when nothing changed and it is of the requested kind, else a new
/// instance of that kind. An edit that leaves the contents unchanged is not a change. Enumeration walks a snapshot.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "SPEC §13.2 names the draft types")]
public sealed class SetDraft<T> : ISet<T>, IReadOnlySet<T>, IRevocable
{
    private readonly IImmutableSet<T> _source;
    private IImmutableSet<T> _current;
    private bool _revoked;

    private SetDraft(IImmutableSet<T> source) => _current = _source = source;

    /// <summary>
    /// Gets a value indicating whether the draft builds a new set: it was changed, unless its edits led back to the source
    /// instance itself (clearing back to a shared empty set), which builds the base.
    /// </summary>
    public bool IsDirty => !ReferenceEquals(Read, _source);

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => Read.Count;

    /// <inheritdoc />
    bool ICollection<T>.IsReadOnly
    {
        get
        {
            _ = Read;
            return false;
        }
    }

    private IImmutableSet<T> Read
    {
        get
        {
            Drafts.ThrowIfRevoked(_revoked, nameof(SetDraft<T>));
            return _current;
        }
    }

    /// <summary>Creates a draft of <paramref name="source"/>.</summary>
    /// <param name="source">The source set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static implicit operator SetDraft<T>(ImmutableHashSet<T> source) => new(source ?? throw new ArgumentNullException(nameof(source)));

    /// <summary>Creates a draft of <paramref name="source"/>.</summary>
    /// <param name="source">The source set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static implicit operator SetDraft<T>(ImmutableSortedSet<T> source) => new(source ?? throw new ArgumentNullException(nameof(source)));

    /// <summary>Builds an <see cref="ImmutableHashSet{T}"/>: the source itself when clean and a hash set.</summary>
    /// <returns>The built set.</returns>
    public ImmutableHashSet<T> BuildHashSet() => Read as ImmutableHashSet<T> ?? [.. _current];

    /// <summary>Builds an <see cref="ImmutableSortedSet{T}"/>: the source itself when clean and a sorted set.</summary>
    /// <returns>The built set.</returns>
    public ImmutableSortedSet<T> BuildSortedSet() => Read as ImmutableSortedSet<T> ?? [.. _current];

    /// <inheritdoc />
    public bool Add(T item) => Apply(Read.Add(item));

    /// <inheritdoc />
    void ICollection<T>.Add(T item) => Add(item);

    /// <inheritdoc />
    public bool Remove(T item) => Apply(Read.Remove(item));

    /// <inheritdoc />
    public void Clear() => Apply(Read.Clear());

    /// <inheritdoc />
    public void UnionWith(IEnumerable<T> other) => Apply(Read.Union(other));

    /// <inheritdoc />
    public void IntersectWith(IEnumerable<T> other) => Apply(Read.Intersect(other));

    /// <inheritdoc />
    public void ExceptWith(IEnumerable<T> other) => Apply(Read.Except(other));

    /// <inheritdoc />
    public void SymmetricExceptWith(IEnumerable<T> other) => Apply(Read.SymmetricExcept(other));

    /// <inheritdoc cref="ICollection{T}.Contains" />
    public bool Contains(T item) => Read.Contains(item);

    /// <inheritdoc cref="ISet{T}.IsSubsetOf" />
    public bool IsSubsetOf(IEnumerable<T> other) => Read.IsSubsetOf(other);

    /// <inheritdoc cref="ISet{T}.IsSupersetOf" />
    public bool IsSupersetOf(IEnumerable<T> other) => Read.IsSupersetOf(other);

    /// <inheritdoc cref="ISet{T}.IsProperSubsetOf" />
    public bool IsProperSubsetOf(IEnumerable<T> other) => Read.IsProperSubsetOf(other);

    /// <inheritdoc cref="ISet{T}.IsProperSupersetOf" />
    public bool IsProperSupersetOf(IEnumerable<T> other) => Read.IsProperSupersetOf(other);

    /// <inheritdoc cref="ISet{T}.Overlaps" />
    public bool Overlaps(IEnumerable<T> other) => Read.Overlaps(other);

    /// <inheritdoc cref="ISet{T}.SetEquals" />
    public bool SetEquals(IEnumerable<T> other) => Read.SetEquals(other);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => ((ICollection<T>)Read).CopyTo(array, arrayIndex); // both kinds are ICollection<T>

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => Read.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    void IRevocable.Revoke() => _revoked = true;

    // Keeps the current set when the edit left the contents unchanged: Intersect rebuilds even when it keeps everything.
    private bool Apply(IImmutableSet<T> next)
    {
        if (next.Count == _current.Count && next.SetEquals(_current))
        {
            return false;
        }

        _current = next;
        return true;
    }
}
