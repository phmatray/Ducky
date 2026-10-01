namespace Ducky;

// SPEC §6.3, §6.7: the init buffer and Ready. Before Ready the main queue holds only System and restore actions, while
// user actions (Local, Effect) wait in the init buffer, never dropped. MarkReady appends StoreInitialized and then the
// buffer under one lock (INV-04): what was queued before Ready runs first, then StoreInitialized, the buffer, and later
// actions. A buffered action keeps the Id it got at enqueue, so Ids are not monotonic in processing order across the gate.
internal sealed partial class Dispatcher
{
    private readonly Queue<Pending> _initBuffer = new();
    private readonly InitCoordinator _initializer = new(inits);

    // The shared init task: StoreInitialized's Pending carries it, so it completes once StoreInitialized is processed,
    // never from MarkReady (another thread may be draining, with StoreInitialized still queued).
    private readonly TaskCompletionSource<DispatchResult> _sharedInit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private StoreState _state;

    internal void StartInit() => _initializer.Start(this);

    // The token cancels only this caller's wait, never init.
    internal Task InitializeAsync(CancellationToken cancellationToken)
    {
        StartInit();
        return _sharedInit.Task.WaitAsync(cancellationToken);
    }

    // Called once, by the initializer, when init completes.
    internal void MarkReady()
    {
        bool drain;
        lock (_gate)
        {
            _state = StoreState.Ready;
            var init = new Pending(new StoreInitialized(), Origin.System, 0, 0, false, false, _sharedInit);
            init.Id = init.CorrelationId = ++_lastId;
            _queue.Enqueue(init);
            while (_initBuffer.TryDequeue(out var buffered))
            {
                _queue.Enqueue(buffered);
            }

            drain = BeginDrainLocked();
        }

        if (drain)
        {
            Drain();
        }
    }

    // Step 6 for a restore: each named slice takes its value when it is the slice's state type. An unknown key or a value
    // of another type restores nothing for that entry (JsonElement values come with M4-06).
    private void Hydrate(HydrateSlices restore)
    {
        _scratch.Clear();
        foreach (var (key, value) in restore.Values)
        {
            if (registry.ByKey.TryGetValue(key, out var ordinal) && registry.Slices[ordinal].TryRestore(value, out var state))
            {
                _scratch.Add((ordinal, state));
            }
        }
    }
}

// Created -> Initializing -> Ready (Disposed comes with M1-11). Initializing is set by the first buffered enqueue,
// the one that flags startInit (§6.3); the other triggers start init without changing it.
internal enum StoreState
{
    Created,
    Initializing,
    Ready,
}

// The batched restore action (§5.7), internal: IStore.Restore copies the values when it is called.
internal sealed record HydrateSlices(IReadOnlyList<KeyValuePair<string, object>> Values);
