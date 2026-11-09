#region search-state
using System.Collections.Immutable;
using Ducky;

namespace DuckShop;

// What the box shows (Query), whether a search is running, and the ids that matched.
// Hits is null when there is no search: the shelf then shows every duck.
public sealed record SearchState(string Query, bool Searching, ImmutableList<string>? Hits);

[DuckyAction]
public sealed record TypeSearch(string Query);

public sealed record SearchAnswered(ImmutableList<string> Hits);

public sealed class SearchSlice : Slice<SearchState>
{
    public SearchSlice()
    {
        On<TypeSearch>((state, action) => string.IsNullOrWhiteSpace(action.Query)
            ? new SearchState(action.Query, Searching: false, Hits: null)
            : state with { Query = action.Query, Searching = true });
        On<SearchAnswered>((state, action) =>
            state with { Searching = false, Hits = action.Hits });
    }

    protected override SearchState Initial => new("", Searching: false, Hits: null);
}
#endregion

#region search-effect
public sealed class SearchEffect(ISearchApi api) : Effect<TypeSearch>
{
    // Switch: a new keystroke cancels the search still running, and a cancelled run's
    // actions are dropped. Only the newest term ever answers.
    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(
        TypeSearch action,
        EffectContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Query))
        {
            return; // an empty box needs no search; this run still cancels the previous one
        }

        var hits = await api.SearchAsync(action.Query.Trim(), cancellationToken);
        context.Dispatch(new SearchAnswered(hits));
    }
}
#endregion
