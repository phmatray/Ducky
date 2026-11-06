using System.Collections.Immutable;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests.Core;

#region effect-state
public sealed record Forecast(string City, int Celsius);

public interface IWeatherApi
{
    Task<Forecast> GetForecastAsync(string city, CancellationToken cancellationToken);
}

public sealed record LoadForecast(string City);

public sealed record ForecastLoading(string City);

public sealed record ForecastLoaded(Forecast Forecast);

public sealed record ForecastFailed(string City, string Error);

public sealed record Weather(ImmutableDictionary<string, Forecast> Forecasts, ImmutableHashSet<string> Loading, string? Error);

public sealed class WeatherSlice : Slice<Weather>
{
    public WeatherSlice()
    {
        On<ForecastLoading>((state, action) => state with { Loading = state.Loading.Add(action.City), Error = null });
        On<ForecastLoaded>((state, action) => state with
        {
            Forecasts = state.Forecasts.SetItem(action.Forecast.City, action.Forecast),
            Loading = state.Loading.Remove(action.Forecast.City),
        });
        On<ForecastFailed>((state, action) => state with { Loading = state.Loading.Remove(action.City), Error = action.Error });
    }

    protected override Weather Initial => new(ImmutableDictionary<string, Forecast>.Empty, [], Error: null);
}
#endregion

// The snippets of docs/guides/core/effects.md.
public sealed class EffectsGuideTests
{
    // The guide's effects live in a private class: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every
    // accessible effect of this assembly, and these inject services that only their own tests register.
    private static class App
    {
        #region effect-load-forecast
        public sealed class LoadForecastEffect(IWeatherApi api) : Effect<LoadForecast>
        {
            // A newer LoadForecast for the same city cancels the running one; other cities load side by side.
            public override Concurrency Policy => Concurrency.Switch;

            protected override object? ConcurrencyKey(LoadForecast action) => action.City;

            public override Task Handle(LoadForecast action, EffectContext context, CancellationToken cancellationToken) =>
                context.Run(
                    started: new ForecastLoading(action.City),
                    work: token => api.GetForecastAsync(action.City, token),
                    succeeded: forecast => new ForecastLoaded(forecast),
                    failed: exception => new ForecastFailed(action.City, exception.Message),
                    cancellationToken);
        }
        #endregion

        #region effect-group
        public sealed record ExportRequested(string Format);

        public sealed record ExportFinished(string Format, Uri File);

        public interface IExportApi
        {
            Task<Uri> ExportAsync(string format, CancellationToken cancellationToken);
        }

        public sealed class ExportEffects : EffectGroup
        {
            public ExportEffects(IExportApi api)
            {
                // Exhaust: requests that arrive while an export runs are ignored.
                On<ExportRequested>(async (action, context, cancellationToken) =>
                {
                    var file = await api.ExportAsync(action.Format, cancellationToken);
                    context.Dispatch(new ExportFinished(action.Format, file));
                }, Concurrency.Exhaust);
            }
        }
        #endregion

        #region effect-poller
        public sealed record Tick;

        public sealed class PollEffect : Effect<Tick>
        {
            public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

            // Switch: the poller's own Tick supersedes the run that dispatched it, and an extra Tick restarts the wait.
            // Exhaust would drop the poller's own Tick (its run still holds the slot) and stop the loop after one round.
            public override Concurrency Policy => Concurrency.Switch;

            // It never finishes: WhenIdleAsync doesn't wait for it.
            public override bool LongRunning => true;

            public override async Task Handle(Tick action, EffectContext context, CancellationToken cancellationToken)
            {
                await Task.Delay(Interval, context.Time, cancellationToken);
                context.Dispatch(new Tick());
            }
        }
        #endregion
    }

    // Non-normative (M15-02): the registered effect loads the forecast through the injected API.
    [Fact]
    public async Task Effects_Registered_LoadsForecast()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var weatherApi = new StubWeatherApi();
        var services = new ServiceCollection();

        #region effect-register
        services.AddSingleton<IWeatherApi>(weatherApi);
        services.AddDucky(ducky => ducky
            .AddSlice<WeatherSlice>()
            .AddEffect<App.LoadForecastEffect>());
        #endregion

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        await store.DispatchAsync(new LoadForecast("Paris"));
        await store.WhenIdleAsync(cancellationToken);

        store.State.Get<Weather>().Error.ShouldBeNull();
        store.State.Get<Weather>().Forecasts["Paris"].ShouldBe(new Forecast("Paris", 21));
        store.State.Get<Weather>().Loading.ShouldBeEmpty();
    }

    // Non-normative (M15-02): Switch keyed by city cancels the earlier run for that city only, and dispatches nothing for it.
    [Fact]
    public async Task Effects_SwitchByCity_SupersededRunCancelledOtherCityRuns()
    {
        var weatherApi = new GatedWeatherApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<WeatherSlice>().AddEffect<App.LoadForecastEffect>(),
            services => services.AddSingleton<IWeatherApi>(weatherApi));

        await store.DispatchAsync(new LoadForecast("Paris"));
        await store.DispatchAsync(new LoadForecast("Rome"));
        await store.DispatchAsync(new LoadForecast("Paris"));
        var (firstParis, rome, secondParis) = (weatherApi.Calls[0], weatherApi.Calls[1], weatherApi.Calls[2]);
        rome.Reply.SetResult(new Forecast("Rome", 25));
        secondParis.Reply.SetResult(new Forecast("Paris", 19));
        await store.Settled();

        weatherApi.Calls.Select(call => call.City).ShouldBe(["Paris", "Rome", "Paris"]);
        firstParis.Token.IsCancellationRequested.ShouldBeTrue();
        rome.Token.IsCancellationRequested.ShouldBeFalse();
        secondParis.Token.IsCancellationRequested.ShouldBeFalse();
        store.State.Get<Weather>().Forecasts.ShouldBe(new Dictionary<string, Forecast>
        {
            ["Paris"] = new("Paris", 19),
            ["Rome"] = new("Rome", 25),
        }, ignoreOrder: true);
        store.State.Get<Weather>().Loading.ShouldBeEmpty();
        store.Failures.ShouldBeEmpty();
        store.Processed.Count(context => context.Action is ForecastFailed or ForecastLoaded).ShouldBe(2);
    }

    // Non-normative (M15-02): an exception from the work dispatches the failed action.
    [Fact]
    public async Task Effects_ApiThrows_FailedActionReduced()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<WeatherSlice>().AddEffect<App.LoadForecastEffect>(),
            services => services.AddSingleton<IWeatherApi>(new StubWeatherApi(new HttpRequestException("offline"))));

        await store.DispatchAsync(new LoadForecast("Paris"));
        await store.Settled();

        store.State.Get<Weather>().Error.ShouldBe("offline");
        store.State.Get<Weather>().Loading.ShouldBeEmpty();
        store.State.Get<Weather>().Forecasts.ShouldBeEmpty();
        store.Failures.ShouldBeEmpty();
    }

    // Non-normative (M15-02): Exhaust ignores requests while an export runs, and accepts the next one after it finished.
    [Fact]
    public async Task Effects_ExhaustGroup_IgnoresRequestsWhileRunning()
    {
        var exportApi = new GatedExportApi();
        await using var store = TestStore.Create(
            ducky =>
            {
                ducky.ThrowOnUnhandledAction = false; // ExportFinished has no reducer here
                ducky.AddEffect<App.ExportEffects>();
            },
            services => services.AddSingleton<App.IExportApi>(exportApi));

        await store.DispatchAsync(new App.ExportRequested("csv"));
        await store.DispatchAsync(new App.ExportRequested("pdf"));
        exportApi.Calls.ShouldBe(["csv"]);
        exportApi.Reply.SetResult(new Uri("https://example.test/export.csv"));
        await store.Settled();
        exportApi.Reply = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        await store.DispatchAsync(new App.ExportRequested("pdf"));
        exportApi.Reply.SetResult(new Uri("https://example.test/export.pdf"));
        await store.Settled();

        exportApi.Calls.ShouldBe(["csv", "pdf"]);
        store.Processed.Select(context => context.Action).OfType<App.ExportFinished>().ShouldBe([
            new App.ExportFinished("csv", new Uri("https://example.test/export.csv")),
            new App.ExportFinished("pdf", new Uri("https://example.test/export.pdf")),
        ]);
        store.Failures.ShouldBeEmpty();
    }

    // Non-normative (M15-02): the Switch poller survives its own Tick: N intervals give N+1 ticks. Under Exhaust the
    // second Tick is dropped by the run that dispatched it, and no third one ever comes.
    [Fact]
    public async Task Effects_SwitchPoller_TicksOncePerInterval()
    {
        const int intervals = 5;
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TickCountSlice>().AddEffect<App.PollEffect>());
        using var reduced = new SemaphoreSlim(0);
        using var selection = store.Select(state => state.Get<TickCount>().Value, _ => reduced.Release());

        await store.DispatchAsync(new App.Tick());
        for (var advanced = 0; advanced <= intervals; advanced++)
        {
            if (advanced > 0)
            {
                store.Time.Advance(App.PollEffect.Interval);
            }

            // A per-step progress bound: the Tick is dispatched on whichever thread resumes the poller's delay.
            (await reduced.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)).ShouldBeTrue($"Tick {advanced + 1} was never reduced.");

            // The drain that reduced it has exited, so the run it started has armed the next delay.
            await store.Settled();
        }

        selection.Value.ShouldBe(intervals + 1);
        store.Processed.Count(context => context.Action is App.Tick).ShouldBe(intervals + 1);
        store.Failures.ShouldBeEmpty();
    }

    private sealed record TickCount(int Value);

    private sealed class TickCountSlice : Slice<TickCount>
    {
        public TickCountSlice() => On<App.Tick>((state, _) => new(state.Value + 1));

        protected override TickCount Initial => new(0);
    }

    private sealed class StubWeatherApi(Exception? failure = null) : IWeatherApi
    {
        public Task<Forecast> GetForecastAsync(string city, CancellationToken cancellationToken) =>
            failure is null ? Task.FromResult(new Forecast(city, 21)) : Task.FromException<Forecast>(failure);
    }

    private sealed class GatedWeatherApi : IWeatherApi
    {
        public List<(string City, TaskCompletionSource<Forecast> Reply, CancellationToken Token)> Calls { get; } = [];

        public Task<Forecast> GetForecastAsync(string city, CancellationToken cancellationToken)
        {
            var reply = new TaskCompletionSource<Forecast>(TaskCreationOptions.RunContinuationsAsynchronously);
            Calls.Add((city, reply, cancellationToken));
            return reply.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class GatedExportApi : App.IExportApi
    {
        public List<string> Calls { get; } = [];

        public TaskCompletionSource<Uri> Reply { get; set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<Uri> ExportAsync(string format, CancellationToken cancellationToken)
        {
            Calls.Add(format);
            return Reply.Task;
        }
    }
}
