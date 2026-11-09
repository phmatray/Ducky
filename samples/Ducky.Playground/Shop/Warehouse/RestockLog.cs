using Ducky.Playground.Shop.Catalogue;

namespace Ducky.Playground.Shop.Warehouse;

// A second slice that reduces AdjustStock. When the catalogue's reducer throws on a malformed adjustment, the store
// discards every slice's result for that action (atomic commit, INV-08): the log never counts an adjustment that failed.
public sealed record RestockLog(int Accepted, int Units);

public sealed class RestockLogSlice : Slice<RestockLog>
{
    public RestockLogSlice() =>
        On<AdjustStock>((state, action) => new RestockLog(state.Accepted + 1, state.Units + action.Delta));

    public override string Key => "restock-log";

    protected override RestockLog Initial => new(Accepted: 0, Units: 0);
}

// The store reports a throwing reducer as ReducerFailed, a system action every slice can reduce like any other.
public sealed record Incidents(string? Last, int Count);

public sealed class IncidentsSlice : Slice<Incidents>
{
    public IncidentsSlice() =>
        On<ReducerFailed>((state, failed) => new Incidents($"{failed.ActionType}: {failed.Exception.Message}", state.Count + 1));

    public override string Key => "incidents";

    protected override Incidents Initial => new(Last: null, Count: 0);
}
