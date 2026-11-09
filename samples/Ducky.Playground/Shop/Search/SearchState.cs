using System.Collections.Immutable;

namespace Ducky.Playground.Shop.Search;

public enum SearchPhase
{
    Idle,
    Waiting,
    Fetching,
}

// Query is what the field shows; Term is what the warehouse is asked. Hits is null while no term is set (the whole shelf).
// Runs counts every keystroke's effect run; Settled the runs that ended on their own (an answer, or an empty term).
public sealed record SearchState(string Query, SearchPhase Phase, ImmutableArray<string>? Hits, int Runs, int Sent, int Answered, int Settled)
{
    public string Term => Normalize(Query);

    // Runs that a newer keystroke superseded (Concurrency.Switch cancelled them): while waiting for a pause, or in flight.
    public int Superseded => Runs - Settled - (Phase == SearchPhase.Idle ? 0 : 1);

    public int CancelledInFlight => Sent - Answered - (Phase == SearchPhase.Fetching ? 1 : 0);

    public static string Normalize(string query) => query.Trim();
}

[DuckyAction]
[ActionType("search/typed")]
public sealed record TypeSearch(string Query);

[ActionType("search/sent")]
public sealed record SearchSent(string Term);

[ActionType("search/answered")]
public sealed record SearchAnswered(string Term, ImmutableArray<string> Hits);

public sealed class SearchSlice : Slice<SearchState>
{
    public SearchSlice()
    {
        On<TypeSearch>((state, action) => SearchState.Normalize(action.Query).Length == 0
            ? state with { Query = action.Query, Phase = SearchPhase.Idle, Hits = null, Runs = state.Runs + 1, Settled = state.Settled + 1 }
            : state with { Query = action.Query, Phase = SearchPhase.Waiting, Runs = state.Runs + 1 });
        On<SearchSent>((state, action) => action.Term == state.Term
            ? state with { Phase = SearchPhase.Fetching, Sent = state.Sent + 1 }
            : state with { Sent = state.Sent + 1 });
        On<SearchAnswered>((state, action) => action.Term == state.Term
            ? state with { Phase = SearchPhase.Idle, Hits = action.Hits, Answered = state.Answered + 1, Settled = state.Settled + 1 }
            : state with { Answered = state.Answered + 1, Settled = state.Settled + 1 });
    }

    public override string Key => "search";

    protected override SearchState Initial => new("", SearchPhase.Idle, Hits: null, Runs: 0, Sent: 0, Answered: 0, Settled: 0);
}
