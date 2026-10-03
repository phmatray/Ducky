namespace Ducky;

// SPEC §6.7 Retire (INV-13, INV-29), called by dispose step 1 (§6.11).
internal sealed partial class InitCoordinator
{
    // Dispose step 1 (§6.7): a Start that has not yet won NotStarted -> Starting loses its CAS and starts nothing; a Start
    // inside its synchronous part starts no further init and arms nothing; a running init ends in a CAS that fails, and its
    // timer never fires. Returns _prefixDone when it replaced Starting (InitTasks is published once that completes), so a
    // dispose called from a synchronous part receives an incomplete task instead of waiting for itself.
#pragma warning disable VSTHRD200 // justification: SPEC §6.7 names it Retire; it hands back a task, it starts no async work
    internal Task Retire()
#pragma warning restore VSTHRD200
    {
        var replaced = Interlocked.Exchange(ref _state, Completed);
        _timer?.Dispose();
        return replaced == Starting ? _prefixDone.Task : Task.CompletedTask;
    }
}
