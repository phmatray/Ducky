using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Ducky.Draft;

/// <summary>
/// A draft of an <see cref="ImmutableDictionary{TKey, TValue}"/> whose values are <see cref="DraftableAttribute">[Draftable]</see>
/// records: reading a value returns its element draft, created lazily on first access and cached by key. <see cref="Build"/>
/// flushes the element drafts and returns the source itself when nothing changed. Replacing or removing a value revokes its
/// cached draft, and revoking this draft revokes every cached element draft. A null value (possible only for an oblivious
/// <typeparamref name="TValue"/>) is never drafted: it reads as null and stays null.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TDraft">The value's draft type.</typeparam>
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix", Justification = "SPEC §13.2 names the draft types")]
public sealed class DictionaryDraft<TKey, TValue, TDraft> : IReadOnlyDictionary<TKey, TDraft>, IRevocable
    where TKey : notnull
    where TValue : class
    where TDraft : class, IDraft<TValue>
{
    private readonly DictionaryDraft<TKey, TValue> _values;
    private readonly Func<TValue, TDraft> _createDraft;
    private readonly Dictionary<TKey, TDraft> _drafts;
    private int _version; // bumped by each removal or Clear: a running enumeration then throws

    /// <summary>Creates a draft of <paramref name="source"/>.</summary>
    /// <param name="source">The source dictionary.</param>
    /// <param name="createDraft">Creates the draft of a value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="createDraft"/> is null.</exception>
    public DictionaryDraft(ImmutableDictionary<TKey, TValue> source, Func<TValue, TDraft> createDraft)
    {
        _values = source; // the conversion throws for a null source
        _createDraft = createDraft ?? throw new ArgumentNullException(nameof(createDraft));
        _drafts = new(source.KeyComparer);
    }

    /// <summary>Gets a value indicating whether the draft may differ from its source.</summary>
    public bool IsDirty => _values.IsDirty || _drafts.Values.Any(draft => draft.IsDirty);

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public IEnumerable<TKey> Keys => _values.Keys;

    /// <summary>Gets the value drafts, walked as <see cref="GetEnumerator"/> walks the entries.</summary>
    public IEnumerable<TDraft> Values => this.Select(pair => pair.Value);

    /// <inheritdoc />
    public TDraft this[TKey key] => Drafts.GetOrCreate(_drafts, _values.StoredKey(key), _values[key], _createDraft); // cached under the stored key

    /// <summary>Sets the value of <paramref name="key"/>, revoking the draft of the value it replaces.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Set(TKey key, TValue value)
    {
        _values[key] = value;
        Drafts.Revoke(_drafts, key);
    }

    /// <summary>Removes <paramref name="key"/>, revoking the draft of its value.</summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether the key was present.</returns>
    public bool Remove(TKey key)
    {
        Drafts.Revoke(_drafts, key);
        var removed = _values.Remove(key);
        if (removed)
        {
            _version++;
        }

        return removed;
    }

    /// <summary>Removes every entry, revoking every cached value draft.</summary>
    public void Clear()
    {
        Drafts.RevokeAll(_drafts);
        _values.Clear();
        _version++;
    }

    /// <summary>Builds the dictionary: the source itself when nothing changed.</summary>
    /// <returns>The built dictionary.</returns>
    public ImmutableDictionary<TKey, TValue> Build()
    {
        foreach (var (key, draft) in _drafts) // cached under the stored key, so the flush never renames a key
        {
            var built = draft.Build();
            if (!ReferenceEquals(built, _values[key])) // by reference: an unchanged value is not a change, and an edit survives the ValueComparer
            {
                _values.Replace(key, built);
            }
        }

        return _values.Build();
    }

    /// <inheritdoc />
    public bool ContainsKey(TKey key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TDraft value)
    {
        var found = _values.TryGetValue(key, out var item);
        value = Drafts.GetOrCreate(_drafts, _values.StoredKey(key), item!, _createDraft); // a missing key leaves item null: never drafted
        return found;
    }

    /// <summary>
    /// Walks a snapshot of the keys, drafting each value on first visit. A removal or <see cref="Clear"/> during the walk makes the
    /// next step throw <see cref="InvalidOperationException"/>, as <see cref="List{T}"/> does; edits through the visited drafts and
    /// <see cref="Set"/> are fine.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<KeyValuePair<TKey, TDraft>> GetEnumerator()
    {
        var version = _version;
        return _values.Keys.Select(key => version == _version ? KeyValuePair.Create(key, this[key]) : throw Drafts.Modified()).GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    void IRevocable.Revoke()
    {
        Drafts.RevokeAll(_drafts);
        ((IRevocable)_values).Revoke();
    }
}
