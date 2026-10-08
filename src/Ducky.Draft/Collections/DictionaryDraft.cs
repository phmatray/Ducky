using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Ducky.Draft;

/// <summary>
/// A lazy copy-on-write draft of an <see cref="ImmutableDictionary{TKey, TValue}"/>: it reads its source until the first
/// change, and every edit goes through the immutable dictionary itself, so the source's <c>KeyComparer</c> and
/// <c>ValueComparer</c> are kept. <see cref="Build"/> returns that source itself when nothing changed. An edit that leaves
/// the contents unchanged (setting an equal value, removing a missing key, clearing an empty draft) is not a change.
/// Enumeration walks a snapshot.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "SPEC §13.2 names the draft types")]
public sealed class DictionaryDraft<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IRevocable
    where TKey : notnull
{
    private readonly ImmutableDictionary<TKey, TValue> _source;
    private ImmutableDictionary<TKey, TValue> _current;
    private bool _revoked;

    private DictionaryDraft(ImmutableDictionary<TKey, TValue> source) => _current = _source = source;

    /// <summary>
    /// Gets a value indicating whether the draft builds a new dictionary: it was changed, unless its edits led back to the source
    /// instance itself (clearing back to a shared empty dictionary), which builds the base.
    /// </summary>
    public bool IsDirty => !ReferenceEquals(Read, _source);

    /// <inheritdoc cref="ICollection{T}.Count" />
    public int Count => Read.Count;

    /// <inheritdoc />
    public IEnumerable<TKey> Keys => Read.Keys;

    /// <inheritdoc />
    public IEnumerable<TValue> Values => Read.Values;

    /// <inheritdoc />
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => ((IDictionary<TKey, TValue>)Read).Keys;

    /// <inheritdoc />
    ICollection<TValue> IDictionary<TKey, TValue>.Values => ((IDictionary<TKey, TValue>)Read).Values;

    /// <inheritdoc />
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
    {
        get
        {
            _ = Read;
            return false;
        }
    }

    private ImmutableDictionary<TKey, TValue> Read
    {
        get
        {
            Drafts.ThrowIfRevoked(_revoked, nameof(DictionaryDraft<TKey, TValue>));
            return _current;
        }
    }

    /// <inheritdoc cref="IDictionary{TKey, TValue}.this[TKey]" />
    public TValue this[TKey key]
    {
        get => Read[key];
        set => _current = Read.SetItem(key, value); // SetItem returns the same instance for an equal value
    }

    /// <summary>Creates a draft of <paramref name="source"/>.</summary>
    /// <param name="source">The source dictionary.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static implicit operator DictionaryDraft<TKey, TValue>(ImmutableDictionary<TKey, TValue> source) =>
        new(source ?? throw new ArgumentNullException(nameof(source)));

    /// <summary>Builds the dictionary: the source itself when clean.</summary>
    /// <returns>The built dictionary.</returns>
    public ImmutableDictionary<TKey, TValue> Build() => Read;

    /// <inheritdoc />
    public void Add(TKey key, TValue value) => _current = Read.Add(key, value); // throws when the key holds another value

    /// <inheritdoc />
    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    /// <inheritdoc />
    public bool Remove(TKey key)
    {
        var next = Read.Remove(key);
        var removed = !ReferenceEquals(next, _current);
        _current = next;
        return removed;
    }

    /// <inheritdoc />
    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Read.Contains(item) && Remove(item.Key);

    /// <inheritdoc />
    public void Clear() => _current = Read.Clear();

    /// <inheritdoc cref="IDictionary{TKey, TValue}.ContainsKey" />
    public bool ContainsKey(TKey key) => Read.ContainsKey(key);

    /// <inheritdoc cref="IDictionary{TKey, TValue}.TryGetValue" />
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => Read.TryGetValue(key, out value);

    /// <inheritdoc />
    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => Read.Contains(item);

    /// <inheritdoc />
    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
        ((ICollection<KeyValuePair<TKey, TValue>>)Read).CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Read.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the key instance the dictionary stores for <paramref name="key"/>, or <paramref name="key"/> when absent.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The stored key.</returns>
    internal TKey StoredKey(TKey key)
    {
        Read.TryGetKey(key, out var stored);
        return stored;
    }

    /// <summary>
    /// Sets <paramref name="value"/> for the present <paramref name="key"/> even when the <c>ValueComparer</c> calls it equal to the
    /// current value, so an element draft's result flushed by reference survives that comparer (INV-26); both comparers are kept.
    /// </summary>
    /// <param name="key">The stored key.</param>
    /// <param name="value">The value.</param>
    internal void Replace(TKey key, TValue value) => _current = Read.Remove(key).Add(key, value);

    /// <inheritdoc />
    void IRevocable.Revoke() => _revoked = true;
}
