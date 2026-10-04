namespace Ducky;

/// <summary>Dispatches actions to the store. Resolved from DI, it forwards to the <see cref="IStore"/> of the same scope.</summary>
/// <remarks>
/// <para>Threading contract:</para>
/// <list type="number">
/// <item><description><c>Dispatch</c>, <c>DispatchAsync</c>, <c>Restore</c>, <c>Select</c>, <c>State</c>,
/// <c>InitializeAsync</c> and <c>WhenIdleAsync</c> may be called from any thread at any time, including from inside
/// a reducer, a middleware hook, a subscriber or an effect, with two exceptions for <c>WhenIdleAsync</c>: from a
/// non-<c>LongRunning</c> effect run of the same store it throws <c>InvalidOperationException</c> (the run counts
/// itself as running, so the wait could never end; another store's run is an unrelated caller, §6.5), and awaited
/// from a middleware's <c>InitializeAsync</c> it waits until <c>InitTimeout</c> (idle requires initialization).
/// None of them ever blocks waiting for another thread, except that concurrent first users wait for the one-time
/// construction of effects and middleware (whose constructors must not block).</description></item>
/// <item><description>Actions are reduced one at a time, never concurrently, in the order in which they were
/// enqueued, with one exception: user actions dispatched before initialization are replayed after
/// <c>StoreInitialized</c>, behind library actions enqueued later (rule 5). Actions from one thread keep their
/// relative order.</description></item>
/// <item><description>If no drain is active, the calling thread becomes the drainer and reduces the whole queue
/// inline on its own thread and synchronization context, including actions enqueued by other threads, before its
/// call returns. Otherwise the call enqueues and returns immediately. <b>A synchronous <c>Dispatch</c> gives
/// read-your-writes only when the caller became the drainer.</b> Use <c>await DispatchAsync(a)</c> for
/// read-after-write. It completes after <c>a</c> is reduced, not after its effects. Use <c>WhenIdleAsync()</c> (or
/// <c>TestStore.Settled()</c>) to also wait for effects.</description></item>
/// <item><description><c>DispatchAsync</c> never faults or cancels, and never completes inline on the drainer. At
/// run time no member throws for a reducer, middleware or effect failure. The only exceptions are programmer
/// errors, thrown synchronously, before any task exists: <c>ArgumentNullException</c> (a null argument); the
/// store's cached <c>DuckyConfigurationException</c> (effects or middleware could not be constructed, DUCKY353);
/// <c>ArgumentOutOfRangeException</c> from <c>Restore</c> with a non-restore origin;
/// <c>InvalidOperationException</c> from a constructor that uses the store during materialization; and misuse,
/// <c>DuckyConfigurationException</c> DUCKY351 (a <c>SliceStore</c> used before the store attached it),
/// <c>InvalidOperationException</c> DUCKY352 (<c>Select</c> after a component's first render) and
/// <c>InvalidOperationException</c> from <c>WhenIdleAsync</c> (or <c>TestStore.Settled</c>) called inside a
/// non-<c>LongRunning</c> effect run (<c>WhenIdle_FromNonLongRunningEffectRun_ThrowsNotHangs</c>). <b>Never block
/// on it</b> (<c>.Result</c>, <c>.Wait()</c>, <c>GetAwaiter().GetResult()</c>): if the caller is the drainer, that
/// is a self-deadlock. DUCKY010 flags it.</description></item>
/// <item><description>Until the store is initialized, user actions are buffered and replayed in order after
/// <c>StoreInitialized</c>. Initialization starts automatically on the first dispatch, select, <c>State</c> read or
/// idle wait.</description></item>
/// <item><description>Reducers, middleware hooks, subscriber callbacks, <c>Select</c> <c>onChange</c> callbacks and
/// the synchronous part of effects run on the draining thread. On Blazor Server that may be a thread-pool thread.
/// Ducky.Blazor's components marshal to the renderer themselves. Custom callbacks must marshal UI work through
/// <c>InvokeAsync</c>.</description></item>
/// <item><description>Reducers, <c>SliceStore</c> mutators and selector projectors must be pure. Projectors may run
/// concurrently on two threads.</description></item>
/// <item><description>A dispatch or restore made while its parent action is being processed (synchronously) is a
/// child with depth + 1, whatever its origin. Chains deeper than <c>MaxDispatchDepth</c> (64) are dropped with
/// <c>ReducerFailed(DispatchLoopException)</c>. A dispatch made after an effect has really yielded, or while the
/// store is idle, starts a new chain.</description></item>
/// <item><description><c>State</c> reads take no lock and always return a complete, immutable
/// snapshot.</description></item>
/// <item><description>After disposal begins, dispatches are ignored and complete with <c>Disposed</c>, and effect
/// tokens are cancelled.</description></item>
/// </list>
/// </remarks>
public interface IDispatcher
{
    /// <summary>
    /// Enqueues <paramref name="action"/> and returns. Never throws for reducer, middleware or effect failures. The action
    /// is reduced before this returns only if the caller became the drainer; use <see cref="DispatchAsync"/> for
    /// read-your-writes.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    void Dispatch(object action);

    /// <summary>
    /// Enqueues <paramref name="action"/>. The task completes, never inline on the drainer, once the action is reduced,
    /// vetoed, dropped or the store is disposed, before the action's effects finish. It never faults and never cancels;
    /// this throws synchronously, before returning a task, only in the cases <see cref="Dispatch"/> throws.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>What happened to the action.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    Task<DispatchResult> DispatchAsync(object action);
}
