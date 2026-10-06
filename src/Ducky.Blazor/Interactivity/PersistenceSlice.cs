namespace Ducky.Blazor;

// The library status slice (SPEC §11.5), registered by AddBlazor. Each store owns its instance, so its DI registration,
// which follows the store's lifetime, is how every party reaches the store's one InteractivityGate (§11.4), from any scope.
internal sealed class PersistenceSlice : Slice<PersistenceState>
{
    public override string Key => "@ducky/persistence";

    public InteractivityGate Gate { get; } = new();

    /// <summary>
    /// Completed once the prerender seed restore has been enqueued or skipped (§11.4): at the handoff's construction
    /// without Prerender&lt;T&gt; slices, in its synchronous init prefix outside the browser, and in the browser once the first
    /// registration or the PrerenderSeedWaitTimeout bound settled the wait. Persistence awaits it before it enqueues
    /// its own restore, so the seed restore always precedes the storage restore (ADR-0011).
    /// </summary>
    public TaskCompletionSource SeedSettled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Always Hydrated: a slice built with new() can't know whether anything is persisted.
    protected override PersistenceState Initial => new(PersistenceStatus.Hydrated);
}
