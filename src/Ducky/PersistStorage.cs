namespace Ducky;

/// <summary>Where a persisted slice is stored. Consumed by Ducky.Blazor; declared here so <see cref="PersistAttribute"/> can use it.</summary>
public enum PersistStorage
{
    /// <summary>The browser's localStorage.</summary>
    Local,

    /// <summary>The browser's sessionStorage.</summary>
    Session,

    /// <summary>The server's <c>IDistributedCache</c>.</summary>
    /// <remarks>
    /// Written through and keyed per user (the scope); a failed write is retried until it succeeds or the store is disposed;
    /// disposal flushes pending writes with one last attempt per key, and a key still unwritten is counted
    /// <c>ducky.persistence.lost</c>. Documented ceilings: the stores of one user (one circuit per tab, a static SSR form post)
    /// share each key with no compare-and-swap and no sync between them, so the last writer wins on whole slices; and a
    /// prerender store's writes, its flush at the end of the request included, can land after the circuit store already
    /// read or wrote the same key, overwriting newer state.
    /// </remarks>
    Server,
}
