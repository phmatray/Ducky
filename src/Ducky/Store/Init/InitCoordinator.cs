namespace Ducky;

// SPEC §6.7, the happy path: NotStarted -> Running -> Completed. Start is idempotent and never runs under _gate; once init
// has started its fast path is one volatile read. Middleware init, InitTimeout, the overflow abort and the rest of Retire
// come with M4-03, M4-03b and M4-03c; until then the init tasks are an internal seam that Ducky.Tests uses to hold init
// Running.
internal sealed class InitCoordinator(Func<Task>[] inits)
{
    private const int NotStarted = 0;
    private const int Running = 1;
    private const int Completed = 2;
    private int _state;

    internal void Start(Dispatcher dispatcher)
    {
        if (Volatile.Read(ref _state) != NotStarted
            || Interlocked.CompareExchange(ref _state, Running, NotStarted) != NotStarted)
        {
            return;
        }

        _ = RunAsync(dispatcher);
    }

    // Starts every init, then steps 4-5. When every init task has already completed this runs synchronously, so the
    // trigger drains inline. An init that faults, synchronously or not, counts as finished, and the next one still starts.
    private async Task RunAsync(Dispatcher dispatcher)
    {
        var tasks = Array.ConvertAll(inits, static init =>
        {
            try
            {
                return init();
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        // Stryker disable once Statement : the fast path treats Running and Completed alike, so nothing reads Completed
        Volatile.Write(ref _state, Completed);
        dispatcher.MarkReady();
    }

    // Dispose step 1 (§6.7): a Start that has not yet won NotStarted -> Running loses its CAS and starts nothing; a running
    // init still ends in MarkReady, which does nothing once Disposed. The timer and _prefixDone come with M4-03c.
    internal void Retire() => Volatile.Write(ref _state, Completed);
}
