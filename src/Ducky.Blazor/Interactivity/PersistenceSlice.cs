namespace Ducky.Blazor;

// The library status slice (SPEC §11.5), registered by AddBlazor. Each store owns its instance, so its DI registration,
// which follows the store's lifetime, is how every party reaches the store's one InteractivityGate (§11.4), from any scope.
internal sealed class PersistenceSlice : Slice<PersistenceState>
{
    public PersistenceSlice()
    {
        // A terminal of another epoch belongs to a superseded attempt: ignored (INV-14, defence in depth).
        On<HydrationCompleted>(static (state, action) => action.ScopeEpoch == state.ScopeEpoch ? state with { Status = PersistenceStatus.Hydrated } : state);
        On<HydrationFailed>(static (state, action) => action.ScopeEpoch == state.ScopeEpoch ? state with { Status = PersistenceStatus.Failed } : state);

        // Storage only (§11.5): handled, so strict mode never reports the public clear as unhandled.
        On<ClearPersistedState>(static state => state);
    }

    public override string Key => "@ducky/persistence";

    public InteractivityGate Gate { get; } = new();

    /// <summary>
    /// Completed once the prerender seed restore has been enqueued or skipped (§11.4): at the handoff's construction
    /// without Prerender&lt;T&gt; slices, in its synchronous init prefix outside the browser, and in the browser once the first
    /// registration or the PrerenderSeedWaitTimeout bound settled the wait. Persistence awaits it before it enqueues
    /// its own restore, so the seed restore always precedes the storage restore (ADR-0011).
    /// </summary>
    public TaskCompletionSource SeedSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The unscoped browser-storage keys a pause seed listed as dirty and restored (§11.4), set before <see cref="SeedSettled"/>
    /// completes: persistence skips their storage restore, and the first terminal takes them and releases them to their writers.
    /// </summary>
    public IReadOnlyList<string> SeedDirty { get; set; } = [];

    // Always Hydrated: a slice built with new() can't know whether anything is persisted.
    protected override PersistenceState Initial => new(PersistenceStatus.Hydrated);

    /// <summary>
    /// The store's persistence middleware once built: its attempt, key set, epoch-to-scope map and baselines are what the
    /// pause seed reads (§11.4).
    /// </summary>
    public PersistenceMiddleware? Middleware { get; set; }
}
