using System.Globalization;

namespace Ducky.Playground.Inspector;

// The result lamp of a tape row (the dispatcher board's lamps): yolk filled for Reduced, beak filled for Dropped and
// Failed, an outlined ring for Vetoed.
public enum Lamp
{
    Reduced,
    Vetoed,
    Dropped,
    Failed,
}

// One row of the inspector. Before and After are the store's immutable snapshots, kept by reference (no copy), so the
// diff is only computed for the row the visitor opens.
public sealed record TapeRow(
    long Number,
    long? Seq,
    string Time,
    string ActionType,
    string Origin,
    Lamp Lamp,
    string? Note,
    object? Action,
    StateSnapshot? Before,
    StateSnapshot? After,
    IReadOnlyList<string> ChangedKeys,
    long CorrelationId);

// The inspector's ring buffer, fed by AuditMiddleware (every reduced action, AfterReduce), by the store's drop counters
// (DropMeter) and by the components that awaited a Vetoed dispatch. It lives outside the store: recording an action never
// dispatches one.
public sealed class Tape(TimeProvider time)
{
    public const int Capacity = 240;

    private readonly Lock _gate = new();
    private readonly TapeRow?[] _rows = new TapeRow?[Capacity];
    private long _written;
    private int _dropped;
    private int _vetoed;
    private int _failed;

    public event Action? Changed;

    public int DroppedCount => Volatile.Read(ref _dropped);

    public int VetoedCount => Volatile.Read(ref _vetoed);

    public int FailedCount => Volatile.Read(ref _failed);

    public long Total => Interlocked.Read(ref _written);

    public void Reduced(ActionContext context) => Write(row => new TapeRow(
        row, context.Id, Now(), context.ActionType, context.Origin.ToString(), Lamp.Reduced, null, context.Action,
        context.PreviousState, context.State, context.ChangedKeys, context.CorrelationId));

    public void Vetoed(string actionType, string reason)
    {
        Interlocked.Increment(ref _vetoed);
        Write(row => new TapeRow(row, null, Now(), actionType, "Local", Lamp.Vetoed, reason, null, null, null, [], 0));
    }

    // ReducerFailed names the action whose reducer threw; that action's row (already written by AfterReduce, with nothing
    // committed) turns Failed.
    public void Failed(string actionType, long correlationId, string message)
    {
        Interlocked.Increment(ref _failed);
        lock (_gate)
        {
            for (var i = _written - 1; i >= Math.Max(0, _written - Capacity); i--)
            {
                var slot = (int)(i % Capacity);
                if (_rows[slot] is { } row && row.ActionType == actionType && row.CorrelationId == correlationId && row.Lamp == Lamp.Reduced)
                {
                    _rows[slot] = row with { Lamp = Lamp.Failed, Note = message };
                    break;
                }
            }
        }
        Changed?.Invoke();
    }

    // An Exhaust drop: the store counts it while it starts the effects of the action it just reduced, which is the newest
    // row (AfterReduce runs before the effects start, SPEC §6.4 steps 9 and 11).
    public void EffectRunDropped()
    {
        Interlocked.Increment(ref _dropped);
        Write(row =>
        {
            var trigger = _rows[(int)((row - 1 + Capacity) % Capacity)];
            return new TapeRow(row, trigger?.Seq, Now(), "effect run dropped", "Effect", Lamp.Dropped,
                trigger is null ? "Exhaust" : $"Exhaust: a run for {trigger.ActionType} is still going", null, null, null, [], trigger?.CorrelationId ?? 0);
        });
    }

    // A superseded Switch run that still tried to dispatch, or a dispatch past MaxDispatchDepth.
    public void DispatchDropped(string reason)
    {
        Interlocked.Increment(ref _dropped);
        Write(row => new TapeRow(row, null, Now(), "dispatch dropped", "Effect", Lamp.Dropped, reason, null, null, null, [], 0));
    }

    // Newest first.
    public IReadOnlyList<TapeRow> Rows()
    {
        lock (_gate)
        {
            var count = (int)Math.Min(_written, Capacity);
            var rows = new List<TapeRow>(count);
            for (var i = _written - 1; i >= _written - count; i--)
            {
                rows.Add(_rows[(int)(i % Capacity)]!);
            }
            return rows;
        }
    }

    public TapeRow? Latest()
    {
        lock (_gate)
        {
            return _written == 0 ? null : _rows[(int)((_written - 1) % Capacity)];
        }
    }

    private void Write(Func<long, TapeRow> create)
    {
        lock (_gate)
        {
            _rows[(int)(_written % Capacity)] = create(_written);
            _written++;
        }
        Changed?.Invoke();
    }

    private string Now() => time.GetLocalNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
