using Ducky.Reactive;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace Ducky.Docs.Tests.Reactive;

#region reactive-search-state
public sealed record Search(string Term, IReadOnlyList<string> Results);

public sealed record SearchTermChanged(string Term);

public sealed record SearchResultsArrived(string Term, IReadOnlyList<string> Results);

public sealed class SearchSlice : Slice<Search>
{
    public SearchSlice()
    {
        On<SearchTermChanged>((state, action) => state with { Term = action.Term });
        On<SearchResultsArrived>((state, action) => action.Term == state.Term ? state with { Results = action.Results } : state);
    }

    protected override Search Initial => new("", []);
}

public interface ISearchApi
{
    Task<IReadOnlyList<string>> SearchAsync(string term, CancellationToken cancellationToken);
}
#endregion

public sealed record Dashboard(bool AutoRefresh, int Refreshes);

public sealed record AutoRefreshToggled(bool On);

public sealed record RefreshDashboard;

public sealed class DashboardSlice : Slice<Dashboard>
{
    public DashboardSlice()
    {
        On<AutoRefreshToggled>((state, action) => state with { AutoRefresh = action.On });
        On<RefreshDashboard>(state => state with { Refreshes = state.Refreshes + 1 });
    }

    protected override Dashboard Initial => new(AutoRefresh: false, Refreshes: 0);
}

public sealed record OrderDetails(int Id, string Status);

public sealed record ShowOrder(int Id);

public sealed record OrderDetailsLoaded(OrderDetails Details);

public sealed class OrdersSlice : Slice<IReadOnlyList<OrderDetails>>
{
    public OrdersSlice() => On<OrderDetailsLoaded>((state, action) => [.. state, action.Details]);

    protected override IReadOnlyList<OrderDetails> Initial => [];
}

public interface IOrdersApi
{
    Task<OrderDetails> GetAsync(int id, CancellationToken cancellationToken);
}

// The snippets of docs/guides/reactive/recipes.md.
public sealed class RecipesGuideTests
{
    // Private: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every accessible reactive effect.
    private static class App
    {
        #region reactive-search
        public sealed class SearchEffect(ISearchApi api, TimeProvider time) : ReactiveEffect
        {
            public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
                actions.OfActionType<SearchTermChanged>()
                    .Debounce(TimeSpan.FromMilliseconds(300), time) // a 300 ms pause in typing
                    .SelectAwait(
                        async (changed, token) => (object)new SearchResultsArrived(changed.Term, await api.SearchAsync(changed.Term, token)),
                        AwaitOperation.Switch); // a newer term cancels the request in flight
        }
        #endregion

        #region reactive-polling
        public sealed class DashboardPolling(TimeProvider time) : ReactiveEffect
        {
            public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
                state.Select(static s => s.Get<Dashboard>().AutoRefresh)
                    .DistinctUntilChanged()
                    .Select(on => on ? Observable.Interval(TimeSpan.FromSeconds(30), time) : Observable.Empty<Unit>())
                    .Switch() // turning the flag off disposes the timer
                    .Select(static object (_) => new RefreshDashboard());
        }
        #endregion

        #region reactive-keyed-switch
        public sealed class OrderDetailsEffect(IOrdersApi api, TimeProvider time) : ReactiveEffect
        {
            public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
                actions.OfActionType<ShowOrder>()
                    // One group per order id, released after a minute without a ShowOrder for it.
                    .GroupByUntil(static show => show.Id, group => group.Debounce(TimeSpan.FromMinutes(1), time).AsUnitObservable())
                    // Within a group, a newer ShowOrder cancels its load in flight; other orders load side by side.
                    .SelectMany(group => group.SelectAwait((show, token) => new ValueTask<OrderDetails>(api.GetAsync(show.Id, token)), AwaitOperation.Switch))
                    .Select(static object (details) => new OrderDetailsLoaded(details));
        }
        #endregion

        #region reactive-register
        public static DuckyBuilder AddSearch(DuckyBuilder ducky) => ducky
            .AddSlice<SearchSlice>()
            .AddReactiveEffect<SearchEffect>(); // created from the store's scope, disposed with the store
        #endregion
    }

    // Non-normative (M15-03): only the term typed last before a 300 ms pause is searched.
    [Fact]
    public async Task DebouncedSearch_SearchesAfterPause()
    {
        var api = new SearchApi();
        await using var store = TestStore.Create(d => App.AddSearch(d), services => services.AddSingleton<ISearchApi>(api));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        foreach (var term in new[] { "d", "du", "duc" })
        {
            await store.DispatchAsync(new SearchTermChanged(term));
            store.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        store.Time.Advance(TimeSpan.FromMilliseconds(199));
        await store.Settled();
        api.Terms.ShouldBeEmpty();

        store.Time.Advance(TimeSpan.FromMilliseconds(1));
        await store.Settled();
        api.Terms.ShouldBe(["duc"]);
        api.Answer(0);
        await store.Settled();
        store.State.Get<Search>().Results.ShouldBe(["duc: yellow duck"]);
    }

    // Non-normative (M15-03): a term searched while the previous search is still in flight cancels that request.
    [Fact]
    public async Task DebouncedSearch_NewerTermCancelsRequestInFlight()
    {
        var api = new SearchApi();
        await using var store = TestStore.Create(d => App.AddSearch(d), services => services.AddSingleton<ISearchApi>(api));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await store.DispatchAsync(new SearchTermChanged("duc"));
        store.Time.Advance(TimeSpan.FromMilliseconds(300));
        await store.DispatchAsync(new SearchTermChanged("duck"));
        store.Time.Advance(TimeSpan.FromMilliseconds(300));

        api.Terms.ShouldBe(["duc", "duck"]);
        api.Pending[0].Token.IsCancellationRequested.ShouldBeTrue();
        api.Pending[1].Token.IsCancellationRequested.ShouldBeFalse();
        api.Answer(1);
        await store.Settled();
        store.State.Get<Search>().Results.ShouldBe(["duck: yellow duck"]);
    }

    // Non-normative (M15-03): the dashboard refreshes every 30 seconds while AutoRefresh is on, and never while it is off.
    [Fact]
    public async Task Polling_OnlyWhileFlagSet()
    {
        await using var store = TestStore.Create(d => d.AddSlice<DashboardSlice>().AddReactiveEffect<App.DashboardPolling>());
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.Time.Advance(TimeSpan.FromMinutes(1));
        await store.Settled();
        store.State.Get<Dashboard>().Refreshes.ShouldBe(0);

        await store.DispatchAsync(new AutoRefreshToggled(true));
        store.Time.Advance(TimeSpan.FromSeconds(30));
        await store.Settled();
        store.State.Get<Dashboard>().Refreshes.ShouldBe(1);
        store.Time.Advance(TimeSpan.FromSeconds(30));
        await store.Settled();
        store.State.Get<Dashboard>().Refreshes.ShouldBe(2);

        await store.DispatchAsync(new AutoRefreshToggled(false));
        store.Time.Advance(TimeSpan.FromMinutes(5));
        await store.Settled();
        store.State.Get<Dashboard>().Refreshes.ShouldBe(2);
    }

    // Non-normative (M15-03): a newer ShowOrder for the same id cancels the load in flight, another id loads beside it.
    [Fact]
    public async Task KeyedSwitch_PerIdSwitch()
    {
        var api = new OrdersApi();
        await using var store = TestStore.Create(
            d => d.AddSlice<OrdersSlice>().AddReactiveEffect<App.OrderDetailsEffect>(),
            services => services.AddSingleton<IOrdersApi>(api));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await store.DispatchAsync(new ShowOrder(1));
        await store.DispatchAsync(new ShowOrder(2));
        await store.DispatchAsync(new ShowOrder(1));
        api.Pending[0].Token.IsCancellationRequested.ShouldBeTrue(); // the first load of order 1
        foreach (var (id, completion, token) in api.Pending.Where(static p => !p.Token.IsCancellationRequested))
        {
            completion.SetResult(new OrderDetails(id, "shipped"));
        }

        await store.Settled();
        store.State.Get<IReadOnlyList<OrderDetails>>().Select(static d => d.Id).Order().ShouldBe([1, 2]);
    }

    // Non-normative (M15-03): the recipe's GroupByUntil releases a group once its duration emits; the key's next item opens
    // a new group, so a long-lived pipeline holds no state for keys it no longer sees.
    [Fact]
    public void GroupByUntil_IdleGroup_Released()
    {
        var time = new FakeTimeProvider();
        using var source = new Subject<int>();
        var (opened, released) = (0, 0);
        using var subscription = source
            .GroupByUntil(static i => i % 2, group => group.Debounce(TimeSpan.FromSeconds(10), time).AsUnitObservable())
            .Subscribe(group =>
            {
                opened++;
                group.Subscribe(static _ => { }, _ => released++);
            });

        source.OnNext(1);
        source.OnNext(2);
        source.OnNext(3);
        (opened, released).ShouldBe((2, 0));

        time.Advance(TimeSpan.FromSeconds(10));
        (opened, released).ShouldBe((2, 2));

        source.OnNext(1);
        (opened, released).ShouldBe((3, 2));
    }

    // Non-normative (M15-03): the source's completion completes every open group, then the outer stream.
    [Fact]
    public void GroupByUntil_SourceCompletes_CompletesOpenGroups()
    {
        using var source = new Subject<int>();
        var completed = new List<string>();
        using var subscription = source.GroupByUntil(static i => i % 2, static _ => Observable.Never<Unit>())
            .Subscribe(g => g.Subscribe(static _ => { }, _ => completed.Add("group")), static _ => { }, _ => completed.Add("outer"));

        source.OnNext(1);
        source.OnNext(2);
        source.OnCompleted();

        completed.ShouldBe(["group", "group", "outer"]);
    }

    // Non-normative (M15-03): a duration that emits on subscribe closes its group after the item that opened it, never
    // before, so that item is delivered.
    [Fact]
    public void GroupByUntil_DurationEmitsOnSubscribe_DeliversItemThenCloses()
    {
        using var source = new Subject<int>();
        var log = new List<string>();
        using var subscription = source.GroupByUntil(static _ => 0, static _ => Observable.Return(Unit.Default))
            .Subscribe(group =>
            {
                log.Add("open");
                group.Subscribe(i => log.Add($"{i}"), _ => log.Add("closed"));
            });

        source.OnNext(1);
        source.OnNext(2);

        log.ShouldBe(["open", "1", "closed", "open", "2", "closed"]);
    }

    // Non-normative (M15-03): a duration that emits during a push nested in another push into the same group closes the
    // group once, after the outer push has returned.
    [Fact]
    public void GroupByUntil_DurationEmitsDuringNestedPush_ClosesOnceAfterOuterPush()
    {
        using var source = new Subject<int>();
        using var closer = new Subject<Unit>();
        var log = new List<string>();
        using var subscription = source.GroupByUntil(static i => i % 2, _ => closer)
            .Subscribe(group =>
            {
                log.Add("open");
                group.Subscribe(
                    i =>
                    {
                        log.Add($"{i}");
                        if (i == 1)
                        {
                            source.OnNext(3); // the same key, pushed from inside the push of 1
                            log.Add("after 3");
                        }
                        else
                        {
                            closer.OnNext(Unit.Default);
                        }
                    },
                    _ => log.Add("closed"));
            });

        source.OnNext(1);
        log.ShouldBe(["open", "1", "3", "after 3", "closed"]);
    }

    // Non-normative (M15-03): a durationSelector that throws releases the group it was opening and fails it, so the key's
    // next item opens a new group instead of joining one that never closes.
    [Fact]
    public void GroupByUntil_DurationSelectorThrows_ReleasesGroup()
    {
        using var source = new Subject<int>();
        var log = new List<string>();
        var calls = 0;
        using var subscription = source.GroupByUntil(static _ => 0, _ => ++calls == 1 ? throw new InvalidOperationException("duration") : Observable.Never<Unit>())
            .Subscribe(
                group =>
                {
                    log.Add("open");
                    group.Subscribe(i => log.Add($"{i}"), result => log.Add(result.IsFailure ? $"failed: {result.Exception.Message}" : "closed"));
                },
                error => log.Add($"error: {error.Message}"),
                static _ => { });

        source.OnNext(1);
        source.OnNext(2);

        log.ShouldBe(["open", "failed: duration", "error: duration", "open", "2"]);
    }

    // Non-normative (M15-03): a duration that ends without emitting, successfully or with a failure, closes its group as
    // Rx.NET's GroupByUntil does, so the key's next item opens a new group instead of joining one that never closes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupByUntil_DurationEndsWithoutEmitting_ClosesGroup(bool fails)
    {
        using var source = new Subject<int>();
        var durations = new List<Subject<Unit>>();
        var log = new List<string>();
        using var subscription = source.GroupByUntil(static _ => 0, _ =>
            {
                var duration = new Subject<Unit>();
                durations.Add(duration);
                return duration;
            })
            .Subscribe(group =>
            {
                log.Add("open");
                group.Subscribe(i => log.Add($"{i}"), _ => log.Add("closed"));
            });

        source.OnNext(1);
        durations[0].OnCompleted(fails ? Result.Failure(new InvalidOperationException("duration")) : Result.Success);
        source.OnNext(2);

        log.ShouldBe(["open", "1", "closed", "open", "2"]);
        durations.Count.ShouldBe(2);
    }

    private sealed class SearchApi : ISearchApi
    {
        public List<(string Term, TaskCompletionSource<IReadOnlyList<string>> Completion, CancellationToken Token)> Pending { get; } = [];

        public IEnumerable<string> Terms => Pending.Select(static p => p.Term);

        public Task<IReadOnlyList<string>> SearchAsync(string term, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<string>>();
            Pending.Add((term, completion, cancellationToken));
            return completion.Task;
        }

        public void Answer(int index) => Pending[index].Completion.SetResult([$"{Pending[index].Term}: yellow duck"]);
    }

    private sealed class OrdersApi : IOrdersApi
    {
        public List<(int Id, TaskCompletionSource<OrderDetails> Completion, CancellationToken Token)> Pending { get; } = [];

        public Task<OrderDetails> GetAsync(int id, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<OrderDetails>();
            Pending.Add((id, completion, cancellationToken));
            return completion.Task;
        }
    }
}
