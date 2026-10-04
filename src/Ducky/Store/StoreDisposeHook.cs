namespace Ducky;

// SPEC §6.11 "Server disposal order": a Scoped store is created before the scoped dependencies its effects and middleware
// pull in at materialization, so its DI scope would dispose them first. Resolved from the store's scope as the last step
// of materialization, this hook is created last and so disposed first: the store drains, awaits its effects and flushes
// while those dependencies are alive, and the container's later disposal of the store is the idempotent no-op.
// Dependencies an effect resolves lazily from an injected IServiceProvider after materialization are not covered.
internal sealed class StoreDisposeHook(IStore store) : IAsyncDisposable, IDisposable
{
    public ValueTask DisposeAsync() => store.DisposeAsync();

    public void Dispose() => store.Dispose();
}
