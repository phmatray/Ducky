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
    /// <para>
    /// In a non-interactive store (static SSR, the prerender pass) the stored value reaches the rendered HTML only when a
    /// lifecycle method awaits <see cref="IStore.InitializeAsync"/>, as <c>DuckyInitializer</c> does in <c>OnInitializedAsync</c>;
    /// a component that only calls <c>Select</c> renders the initial state, and on a static SSR page that HTML is the only
    /// output. The prerender seed carries the value in any case. The flush of a static SSR form post runs after the redirect
    /// response was sent, so the browser's immediate GET can render the value from before the post (Post/Redirect/Get).
    /// A cache that completes <c>GetAsync</c> synchronously, such as <c>MemoryDistributedCache</c>, restores before the render
    /// and hides this; don't rely on it, because an asynchronous cache such as Redis renders the initial state.
    /// </para>
    /// </remarks>
    Server,
}
