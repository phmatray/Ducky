namespace Ducky;

// SPEC §6.7 RequestOverflowAbort (INV-05, INV-13): an init buffer past its soft bound aborts a Running init. The abort is
// handed to QueueWorkItem, never run on the caller: Abort cancels the init token, whose callbacks may wait (persistence
// waits for its _issue lock), and no Dispatch, State read or Select may wait for them.
internal sealed partial class Dispatcher
{
    // Default soft bound of the init buffer (§5.1); DuckyBuilder.InitBufferCapacity sets it.
    internal const int DefaultInitBufferCapacity = 1024;

    // IVT-only seam (§6.7, §17.1): defaults to the production pool hop; Ducky.Tests captures the abort to run it at a chosen point.
    internal Action<Action> QueueWorkItem { get; set; } = static work => ThreadPool.UnsafeQueueUserWorkItem(static w => ((Action)w!)(), work); // the global queue (preferLocal: false)

    // Start step 3: the buffer is read under _gate.
    internal bool InitBufferOverflowed
    {
        get
        {
            lock (_gate)
            {
                return _initBuffer.Count > initBufferCapacity;
            }
        }
    }
}

internal sealed partial class InitCoordinator
{
    private int _overflowAbortQueued;

    // From Enqueue and Start step 3. Only from Running and only once: from NotStarted or Starting it does nothing and leaves
    // the flag unset, so Start step 3 re-checks; from Completed it is one volatile read, and with an abort already queued a
    // failed CAS. The queued Abort loses its CAS when init completed meanwhile.
    internal void RequestOverflowAbort(Dispatcher dispatcher)
    {
        if (Volatile.Read(ref _state) == Running && Interlocked.CompareExchange(ref _overflowAbortQueued, 1, 0) == 0)
        {
            dispatcher.QueueWorkItem(() => Abort(dispatcher, InitAbortReason.BufferOverflow));
        }
    }
}
