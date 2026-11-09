// Live search on a Switch effect: a newer keystroke cancels the run in progress,
// whether it is still waiting out the pause or already asking the warehouse.
using Ducky.Playground.Shop.Api;

namespace Ducky.Playground.Shop.Search;

public sealed class SearchEffect(ShopApi api) : Effect<TypeSearch>
{
    // The debounce: a run waits this long, on the store's TimeProvider, before it asks.
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    // A cancelled run dispatches nothing more, so a slow answer to an old term can never
    // overwrite the answer to the newer one.
    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(
        TypeSearch action,
        EffectContext context,
        CancellationToken cancellationToken)
    {
        var term = SearchState.Normalize(action.Query);
        if (term.Length == 0)
        {
            return;
        }

        await Task.Delay(Pause, context.Time, cancellationToken);
        context.Dispatch(new SearchSent(term));
        var hits = await api.SearchAsync(term, cancellationToken);
        context.Dispatch(new SearchAnswered(term, hits));
    }
}
