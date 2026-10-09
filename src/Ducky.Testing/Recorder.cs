using System.Collections.Immutable;

namespace Ducky.Testing;

// The lists behind TestStore.Processed and Failures (SPEC §15): appended on the drainer, which can be any thread, and
// read by the test thread, so each is an immutable list swapped atomically and read as a snapshot.
internal sealed class Recorder(Type[] failureTypes)
{
    private readonly Type[] _failureTypes = [.. failureTypes];
    private ImmutableList<ActionContext> _processed = [];
    private ImmutableList<object> _failures = [];

    public ImmutableList<ActionContext> Processed => Volatile.Read(ref _processed);

    public ImmutableList<object> Failures => Volatile.Read(ref _failures);

    public void Record(ActionContext context)
    {
        ImmutableInterlocked.Update(ref _processed, static (list, item) => list.Add(item), context);
        if (context.Action is ReducerFailed or EffectFailed || _failureTypes.Contains(context.Action.GetType()))
        {
            ImmutableInterlocked.Update(ref _failures, static (list, item) => list.Add(item), context.Action);
        }
    }
}

// Created by the store from its scope; the container's one Recorder is injected.
internal sealed class RecordingMiddleware(Recorder recorder) : Middleware
{
    public override void AfterReduce(ActionContext context) => recorder.Record(context);
}
