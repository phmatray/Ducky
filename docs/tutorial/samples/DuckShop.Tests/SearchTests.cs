using Ducky;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckShop.Tests;

// The search box's effect (tutorial step 6): Switch keeps only the newest search.
public sealed class SearchTests
{
    [Fact]
    public async Task TypeSearch_TypedFast_OnlyTheNewestTermAnswers()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<SearchSlice>().AddEffect<SearchEffect>(),
            services => services.AddSingleton<ISearchApi, FakeSearchApi>());

        store.TypeSearch("y");
        store.TypeSearch("ye");
        store.TypeSearch("yel");
        store.State.Get<SearchState>().Searching.ShouldBeTrue();
        store.Time.Advance(FakeSearchApi.Latency("y")); // long enough for all three
        await store.Settled();

        store.State.Get<SearchState>().Query.ShouldBe("yel");
        store.State.Get<SearchState>().Searching.ShouldBeFalse();
        store.State.Get<SearchState>().Hits.ShouldBe(["classic", "mellow"]);
        store.Processed.Count(context => context.Action is SearchAnswered).ShouldBe(1);
    }

    // Why not Merge: every search runs to the end, and the slowest one ("y", three ducks)
    // answers last, so the shelf would show three ducks for "yel".
    [Fact]
    public async Task TypeSearch_UnderMerge_TheStaleAnswerWins()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<SearchSlice>().AddEffect<MergeSearchEffect>(),
            services => services.AddSingleton<ISearchApi, FakeSearchApi>());

        store.TypeSearch("y");
        store.TypeSearch("ye");
        store.TypeSearch("yel");
        store.Time.Advance(FakeSearchApi.Latency("y"));
        await store.Settled();

        store.State.Get<SearchState>().Query.ShouldBe("yel");
        store.State.Get<SearchState>().Hits.ShouldBe(["classic", "mellow", "rubber"]);
        store.Processed.Count(context => context.Action is SearchAnswered).ShouldBe(3);
    }

    // Clearing the box cancels the search in flight: its answer never lands.
    [Fact]
    public async Task TypeSearch_Cleared_InFlightSearchDropped()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<SearchSlice>().AddEffect<SearchEffect>(),
            services => services.AddSingleton<ISearchApi, FakeSearchApi>());

        store.TypeSearch("pirate");
        store.TypeSearch("");
        store.Time.Advance(FakeSearchApi.Latency("y"));
        await store.Settled();

        store.State.Get<SearchState>().ShouldBe(new SearchState("", Searching: false, Hits: null));
        store.Processed.ShouldNotContain(context => context.Action is SearchAnswered);
    }

    // SearchEffect with the default policy, Merge: the bug Switch prevents.
    private sealed class MergeSearchEffect(ISearchApi api) : Effect<TypeSearch>
    {
        public override async Task Handle(TypeSearch action, EffectContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(action.Query))
            {
                return;
            }

            var hits = await api.SearchAsync(action.Query.Trim(), cancellationToken);
            context.Dispatch(new SearchAnswered(hits));
        }
    }
}
