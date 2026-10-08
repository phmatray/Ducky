namespace Ducky.Draft;

/// <summary>Helpers shared by the collection drafts.</summary>
internal static class Drafts
{
    /// <summary>Throws the revoked-draft <see cref="ObjectDisposedException"/> when <paramref name="revoked"/> is set.</summary>
    public static void ThrowIfRevoked(bool revoked, string draftName)
    {
        if (revoked)
        {
            throw new ObjectDisposedException(
                draftName,
                "draft revoked: Produce returned, or an assignment or structural operation replaced it");
        }
    }

    /// <summary>The exception an element-draft enumerator throws when a structural operation ran during the walk, as <c>List&lt;T&gt;</c> does.</summary>
    public static InvalidOperationException Modified() =>
        new("collection was modified: a structural operation ran during enumeration of the element drafts");

    /// <summary>Returns the cached element draft of <paramref name="item"/>, creating it on first access; null for a null item.</summary>
    public static TDraft GetOrCreate<TKey, T, TDraft>(Dictionary<TKey, TDraft> drafts, TKey key, T item, Func<T, TDraft> createDraft)
        where TKey : notnull
        where T : class
        where TDraft : class
    {
        if (item is null)
        {
            return null!; // an oblivious element can be null: it is never drafted (§13.2)
        }

        if (!drafts.TryGetValue(key, out var draft))
        {
            drafts[key] = draft = createDraft(item);
        }

        return draft;
    }

    /// <summary>Revokes and forgets the cached element draft of <paramref name="key"/>, if any.</summary>
    public static void Revoke<TKey, TDraft>(Dictionary<TKey, TDraft> drafts, TKey key)
        where TKey : notnull
        where TDraft : IRevocable
    {
        if (drafts.Remove(key, out var stale))
        {
            stale.Revoke();
        }
    }

    /// <summary>Revokes and forgets every cached element draft.</summary>
    public static void RevokeAll<TKey, TDraft>(Dictionary<TKey, TDraft> drafts)
        where TKey : notnull
        where TDraft : IRevocable
    {
        foreach (var draft in drafts.Values)
        {
            draft.Revoke();
        }

        drafts.Clear();
    }
}
