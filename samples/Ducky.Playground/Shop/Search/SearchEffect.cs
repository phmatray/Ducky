using Ducky.Playground.Shop.Api;

namespace Ducky.Playground.Shop.Search;

// Every keystroke dispatches TypeSearch. Switch: a newer keystroke cancels the run in progress, whether it is still
// waiting out the pause (the debounce, on the store's TimeProvider) or already waiting for the warehouse. A cancelled
// run dispatches nothing more, so a slow answer to an old term can never overwrite the newer one.
public sealed class SearchEffect(ShopApi api) : Effect<TypeSearch>
{
    public static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(TypeSearch action, EffectContext context, CancellationToken cancellationToken)
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
