namespace Ducky;

// SPEC §6.6 "Materialization and disposal" (INV-29, INV-31): materialization is either never started, and then never
// starts once disposal began, or in flight or done, and then disposal awaits it and disposes what it built.
internal sealed partial class Dispatcher
{
    // Completed by Build's finally (after the cleanup of a throwing factory); DisposeAsync awaits it once materialization
    // had started before step 1 (§6.11 step 4).
    private readonly TaskCompletionSource _materializedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _materializationStarted; // read and written only under _gate

    // What a successful factory built, published before _materializedSignal completes: disposal reads it, never the Lazy,
    // which publishes its value only after the factory returned.
    private Materialized? _built;

    // Called first by every materializing entry point. Once done, one volatile read. Once the store is Disposed nothing is
    // built and nothing rethrown: the caller takes its after-disposal path, also when the factory threw after that. A throw
    // while disposal has begun but not yet set Disposed is rethrown: every entry point's own check still sees a live store.
    internal void Materialize()
    {
        if (_materialized.IsValueCreated || !BeginMaterialize())
        {
            return;
        }

        try
        {
            _ = _materialized.Value;
        }
        catch (Exception) when (IsDisposed())
        {
            // The after-disposal path: DisposeAsync's caller never sees DUCKY353 either (§6.6).
        }
    }

    // The Lazy's factory: store-owned effects, then middleware, in registration order, then the dispose hook, skipped once
    // the store is Disposed (it exists only to start a disposal). A throw disposes the store-owned instances built so far
    // before the Lazy caches it: every disposal is started, in reverse construction order and each in its own try/catch,
    // before the rethrow, and the rest of an asynchronous one is awaited by DisposeAsync's materialization wait
    // (_materializedSignal): blocking on user code here is forbidden (INV-05). Middleware are bounded as in phase 5a
    // (Warning 1016), so a hung DisposeAsync never holds the store scope. They run under the Lazy's lock, so one that uses
    // the store throws InvalidOperationException (Error 1015).
    internal Materialized Build(
        Func<IEnumerable<(Effect Effect, bool Owned)>> effects,
        Func<IEnumerable<Middleware>> middleware,
        Func<object>? disposeHook)
    {
        List<object> built = [];
        var cleanup = Task.CompletedTask;
        try
        {
            List<(Effect Effect, bool Owned)> allEffects = [];
            foreach (var effect in effects())
            {
                allEffects.Add(effect);
                if (effect.Owned)
                {
                    built.Add(effect.Effect);
                }
            }

            List<Middleware> allMiddleware = [];
            foreach (var m in middleware())
            {
                allMiddleware.Add(m);
                built.Add(m);
            }

            var materialized = new Materialized([.. allEffects], [.. allMiddleware]);
            if (!IsDisposed())
            {
                try
                {
                    _ = disposeHook?.Invoke();
                }
                catch (ObjectDisposedException)
                {
                    // The store's scope is disposing (a first use from a scoped service's disposal): it disposes the store
                    // next, which is all the hook would start (§6.11).
                }
            }

            Volatile.Write(ref _built, materialized);
            return materialized;
        }
        catch
        {
            var disposals = new Task[built.Count];
            for (var i = built.Count - 1; i >= 0; i--)
            {
                disposals[i] = built[i] is Middleware m ? DisposeBoundedAsync(m) : DisposeOneAsync(built[i]);
            }

            cleanup = Task.WhenAll(disposals);
            throw;
        }
        finally
        {
            _ = SignalAfterAsync(cleanup);
        }
    }

    // Under _gate, in the critical section where DisposeAsync step 1 sets Disposed (§6.11).
    private bool BeginMaterialize()
    {
        lock (_gate)
        {
            if (_state == StoreState.Disposed)
            {
                return false;
            }

            _materializationStarted = true;
            return true;
        }
    }

    // The predicate of every entry point's after-disposal check, never DisposalBegan: DisposeAsync publishes its task
    // before step 1 sets Disposed. No user code runs under the lock.
    private bool IsDisposed()
    {
        lock (_gate)
        {
            return _state == StoreState.Disposed;
        }
    }

    // The cleanup never faults: each disposal is in its own try/catch.
    private async Task SignalAfterAsync(Task cleanup)
    {
#pragma warning disable VSTHRD003 // justification: our own disposals, which never fault
        await cleanup.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        _materializedSignal.TrySetResult();
    }
}
