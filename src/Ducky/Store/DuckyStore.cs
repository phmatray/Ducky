using Microsoft.Extensions.Logging;

namespace Ducky;

// The store facade (SPEC §5.2). Construction is internal until DuckyStore.Create and AddDucky arrive (M1-08, M1-09).
internal sealed class DuckyStore
{
    internal DuckyStore(IEnumerable<Slice> slices, ILogger logger)
    {
        Slice[] owned = [.. slices];
        foreach (var slice in owned)
        {
            slice.Freeze();
        }

        var registry = new Registry(owned);
        Slices = Array.AsReadOnly(owned);
        InitialState = new StateSnapshot(registry);
        Dispatcher = new Dispatcher(registry, InitialState, new SafeLogger(logger));
    }

    // Registry data: reading it starts nothing.
    public IReadOnlyList<Slice> Slices { get; }

    public StateSnapshot InitialState { get; }

    public StateSnapshot State => Dispatcher.State;

    internal Dispatcher Dispatcher { get; }

    public void Dispatch(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.Enqueue(Dispatcher.NewPending(action, Origin.Local, null));
    }

    public Task<DispatchResult> DispatchAsync(object action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.Enqueue(Dispatcher.NewPending(action, Origin.Local, completion));
        return completion.Task;
    }
}
