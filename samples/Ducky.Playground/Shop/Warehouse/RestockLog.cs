// A second slice reduces AdjustStock. If the catalogue's reducer throws on a malformed
// adjustment, the store discards every slice's result: one action, one atomic commit.
using Ducky.Playground.Shop.Catalogue;

namespace Ducky.Playground.Shop.Warehouse;

// Accepted counts the adjustments committed; Units is their total delta.
public sealed record RestockLog(int Accepted, int Units);

public sealed class RestockLogSlice : Slice<RestockLog>
{
    // Never counts an adjustment that failed: the catalogue's throw discards this
    // result too.
    public RestockLogSlice() => On<AdjustStock>((state, action) =>
        new RestockLog(state.Accepted + 1, state.Units + action.Delta));

    public override string Key => "restock-log";

    protected override RestockLog Initial => new(Accepted: 0, Units: 0);
}

// The store reports a throwing reducer as ReducerFailed, a system action that any slice
// can reduce like its own.
public sealed record Incidents(string? Last, int Count);

public sealed class IncidentsSlice : Slice<Incidents>
{
    public IncidentsSlice() => On<ReducerFailed>((state, failed) => new Incidents(
        $"{failed.ActionType}: {failed.Exception.Message}",
        state.Count + 1));

    public override string Key => "incidents";

    protected override Incidents Initial => new(Last: null, Count: 0);
}
