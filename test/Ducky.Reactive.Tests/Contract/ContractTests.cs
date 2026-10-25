using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Reactive.Tests.Contract;

// SPEC §6.3 (SafeLogger: Ducky.Reactive's own copy), §6.4 (StoreInitialized's effects start after a reducer failure), §5.1
// and §14 (DUCKY309 for reactive effects); INV-03, INV-13, INV-31.
public sealed class ContractTests
{
    [Fact]
    public async Task ReducerThrowsOnStoreInitialized_LoadEffectAndReactiveHostStillStart()
    {
        // StoreInitialized's reducer throws: it commits nothing and completes Failed, but its effects react to the action,
        // not to the commit, so the load effect runs and ReactiveHost subscribes the pipelines (§6.4).
        await using var host = new Host(d => d.AddSlice<CounterSlice>().AddSlice<FailureSlice>().AddSlice<ThrowOnInitSlice>()
            .AddEffect<LoadOnInitEffect>().AddReactiveEffect<LateRecordingEffect>());

        await host.StartAsync();

        host.Store.State.Get<IReadOnlyList<ReducerFailed>>().ShouldHaveSingleItem().ActionType.ShouldBe(typeof(StoreInitialized).ToString());
        host.Store.State.Get<Counter>().Loaded.ShouldBeTrue();
        host.Probe.Seen.ShouldHaveSingleItem().ShouldBeOfType<StoreInitialized>();

        // The pipeline is live: it sees the next action too.
        (await host.Store.DispatchAsync(new Ping())).ShouldBe(DispatchResult.Reduced);
        host.Probe.Seen.Count.ShouldBe(2);
        host.Probe.Seen[1].ShouldBeOfType<Ping>();
    }

    [Fact]
    public async Task SafeLogger_ProviderThrows_NeverEscapes()
    {
        // Every non-fatal throw is swallowed: IsEnabled then says false and BeginScope returns a no-op scope.
        var failing = new ThrowingLogger<object>(_ => new InvalidOperationException("log"))
        {
            OnIsEnabled = new AggregateException("enabled"),
            OnBeginScope = new InvalidOperationException("scope"),
        };
        var safe = new SafeLogger(failing);
#pragma warning disable CA1848 // justification: the test calls ILogger.Log directly, as a provider sees it
        safe.LogWarning(new EventId(7), "swallowed");
#pragma warning restore CA1848
        failing.Logged.ShouldBe([7]);
        safe.IsEnabled(LogLevel.Error).ShouldBeFalse();
        safe.BeginScope("scope").ShouldNotBeNull().Dispose();

        // OutOfMemoryException is fatal (§10): it passes through every member.
        var fatal = new ThrowingLogger<object>(_ => ThrowingLogger<object>.Fatal())
        {
            OnIsEnabled = ThrowingLogger<object>.Fatal(),
            OnBeginScope = ThrowingLogger<object>.Fatal(),
        };
        var unsafeFatal = new SafeLogger(fatal);
#pragma warning disable CA1848 // justification: as above
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.LogWarning(new EventId(8), "fatal"));
#pragma warning restore CA1848
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.IsEnabled(LogLevel.Error));
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.BeginScope("scope"));

        // A healthy provider sees every call unchanged.
        var scope = new MemoryStream();
        var healthy = new SafeLogger(new ThrowingLogger<object>(_ => null) { Enabled = false, Scope = scope });
        healthy.IsEnabled(LogLevel.Error).ShouldBeFalse();
        new SafeLogger(new ThrowingLogger<object>(_ => null)).IsEnabled(LogLevel.Error).ShouldBeTrue();
        healthy.BeginScope("scope").ShouldBeSameAs(scope);

        // ReactiveHost wraps its logger once, at construction: its EventId 3000 log throws inside the provider, and the
        // pipelines still subscribe.
        var provider = new ThrowingLogger<ReactiveHost>(_ => new InvalidOperationException("provider"));
        await using (var host = new Host(d => d.AddReactiveEffect<FirstStateEffect>(), s => s.AddSingleton<ILogger<ReactiveHost>>(provider)))
        {
            await host.StartAsync();
            host.Probe.Seen.ShouldHaveSingleItem();
        }

        provider.Logged.ShouldBe([3000]);

        // A healthy provider gets EventId 3000 at Debug, naming the effect type, once per subscribed reactive effect.
        var fake = new FakeLogger<ReactiveHost>();
        await using (var host = new Host(d => d.AddReactiveEffect<FirstStateEffect>().AddReactiveEffect<PingLoopEffect>(), s => s.AddSingleton<ILogger<ReactiveHost>>(fake)))
        {
            await host.StartAsync();
        }

        var records = fake.Collector.GetSnapshot();
        records.Select(static r => (r.Id.Id, r.Level)).ShouldBe([(3000, LogLevel.Debug), (3000, LogLevel.Debug)]);
        records.Select(static r => r.Message).ShouldBe(
            [$"Reactive effect {typeof(FirstStateEffect)} subscribed", $"Reactive effect {typeof(PingLoopEffect)} subscribed"]);
    }

    [Fact]
    public void Build_ReactiveEffectCtorDependencyMissing_ReportedWithOtherErrors()
    {
        // The core never sees the reactive effect type, so AddReactiveEffect queues it: an unresolvable constructor
        // parameter is DUCKY309 naming the reactive effect, reported at first resolution with the other errors, not
        // DUCKY353 naming ReactiveHost at first use.
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddReactiveEffect<MissingDependencyEffect>().AddReactiveEffect<PingLoopEffect>().AddValidation(static _ => [new DuckyError("TEST001", "other", "fix", "link")]));
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<DuckyConfigurationException>(() => provider.GetRequiredService<IStore>());

        exception.Errors.Select(static e => e.Code).ShouldBe(["DUCKY309", "TEST001"], ignoreOrder: true);
        var ctor = exception.Errors.Single(static e => e.Code == "DUCKY309");
        ctor.Message.ShouldContain(nameof(MissingDependencyEffect));
        ctor.Message.ShouldContain(nameof(Unregistered));
        ctor.Message.ShouldContain("AddReactiveEffect<T>");
    }
}
