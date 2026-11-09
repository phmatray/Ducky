// The inspector's ring buffer. It lives outside the store, so recording an action never
// dispatches one: AuditMiddleware, DropMeter and vetoed components write to it directly.
using System.Globalization;

namespace Ducky.Playground.Inspector;

// A row's result lamp: yolk for Reduced, beak for Dropped and Failed, a ring for Vetoed.
public enum Lamp
{
    Reduced,
    Vetoed,
    Dropped,
    Failed,
}

// One row of the inspector. Sequence is the store's action id; Number is the row's own.
// Before and After are the store's immutable snapshots, kept by reference without a
// copy, so a diff is computed only for the row the visitor opens.
public sealed record TapeRow(
    long Number,
    long? Sequence,
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

    public void Reduced(ActionContext context) => Write(number => new TapeRow(
        number,
        context.Id,
        Now(),
        context.ActionType,
        context.Origin.ToString(),
        Lamp.Reduced,
        Note: null,
        context.Action,
        context.PreviousState,
        context.State,
        context.ChangedKeys,
        context.CorrelationId));

    public void Vetoed(string actionType, string reason)
    {
        Interlocked.Increment(ref _vetoed);
        Write(number =>
            Unreduced(number, null, actionType, "Local", Lamp.Vetoed, reason, 0));
    }

    // ReducerFailed names the action whose reducer threw. AfterReduce already wrote that
    // action's row, with nothing committed: it turns Failed.
    public void Failed(string actionType, long correlationId, string message)
    {
        Interlocked.Increment(ref _failed);
        lock (_gate)
        {
            for (var i = _written - 1; i >= Math.Max(0, _written - Capacity); i--)
            {
                var slot = (int)(i % Capacity);
                if (_rows[slot] is { Lamp: Lamp.Reduced } row
                    && row.ActionType == actionType
                    && row.CorrelationId == correlationId)
                {
                    _rows[slot] = row with { Lamp = Lamp.Failed, Note = message };
                    break;
                }
            }
        }
        Changed?.Invoke();
    }

    // The store counts an Exhaust drop while starting the effects of the action it just
    // reduced; AfterReduce ran before that, so that action is the newest row.
    public void EffectRunDropped()
    {
        Interlocked.Increment(ref _dropped);
        Write(number =>
        {
            var trigger = _rows[(int)((number - 1 + Capacity) % Capacity)];
            var note = trigger is null
                ? "Exhaust"
                : $"Exhaust: a run for {trigger.ActionType} is still going";
            return Unreduced(
                number,
                trigger?.Sequence,
                "effect run dropped",
                "Effect",
                Lamp.Dropped,
                note,
                trigger?.CorrelationId ?? 0);
        });
    }

    public void DispatchDropped(string reason)
    {
        Interlocked.Increment(ref _dropped);
        Write(number => Unreduced(
            number, null, "dispatch dropped", "Effect", Lamp.Dropped, reason, 0));
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

    // A row for something the store never reduced: no action, snapshots or changed keys.
    private TapeRow Unreduced(
        long number,
        long? sequence,
        string actionType,
        string origin,
        Lamp lamp,
        string note,
        long correlationId) =>
        new(number, sequence, Now(), actionType, origin, lamp, note,
            Action: null, Before: null, After: null, ChangedKeys: [], correlationId);

    private void Write(Func<long, TapeRow> createRow)
    {
        lock (_gate)
        {
            _rows[(int)(_written % Capacity)] = createRow(_written);
            _written++;
        }
        Changed?.Invoke();
    }

    private string Now() =>
        time.GetLocalNow().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
}
