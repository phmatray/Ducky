using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §5.5 (EffectGroup: one runner per On<T>, each with its own policy and key, constructor only), §5.1 and §6.6
// (a throwing EffectGroup constructor is DUCKY353 naming DUCKY307/DUCKY308), §6.3 and §6.4 step 11 (a throwing key
// function fails only its own effect); INV-11, INV-31.
public sealed class EffectGroupTests
{
    // A throwing ConcurrencyKey and a throwing EffectGroup key function, next to a healthy effect: each throw is that
    // effect's EffectFailed, in registration order, and the healthy effect still starts.
    [Fact]
    public async Task Effect_ConcurrencyKeyThrows_EffectFailedOtherEffectsStart()
    {
        var keyThrew = new FormatException("key");
        var groupKeyThrew = new ArgumentException("group key");
        List<string> handled = [];
        var keyed = new SwitchHandler<Load>(
            (_, _, _) =>
            {
                handled.Add("keyed");
                return Task.CompletedTask;
            },
            key: _ => throw keyThrew);
        var group = new Group(g => g.Register<Load>(
            (_, _, _) =>
            {
                handled.Add("group");
                return Task.CompletedTask;
            },
            Concurrency.Switch,
            _ => throw groupKeyThrew));
        var healthy = new Handler<Load>((_, _, _) =>
        {
            handled.Add("healthy");
            return Task.CompletedTask;
        });
        var store = new DuckyStore(
            [new SeenSlice()], NullLogger.Instance, effects: () => [(keyed, false), (group, false), (healthy, false)]);

        store.Dispatch(new Load(1));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        handled.ShouldBe(["healthy"]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        var load = typeof(Load).ToString();
        store.State.Get<Seen>().Actions.ShouldBe(
        [
            new Load(1),
            new EffectFailed(typeof(SwitchHandler<Load>).ToString(), load, keyThrew),
            new EffectFailed(typeof(Group).ToString(), load, groupKeyThrew),
        ]);
    }

    // Each On<T> is its own runner: Load runs Switch per key (Load 3 supersedes Load 1 only), Loaded runs Merge with the
    // store-lifetime token, and Derived runs Switch without a key, in one global slot.
    [Fact]
    public async Task EffectGroup_PolicyPerHandler()
    {
        var gate = new TaskCompletionSource();
        List<(object Action, CancellationToken Token)> runs = [];
        var group = new Group(g =>
        {
            g.Register<Load>(Record, Concurrency.Switch, load => load.Id % 2);
            g.Register<Loaded>(Record);
            g.Register<Derived>(Record, Concurrency.Switch);
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(group, false)]);
        var first = new Derived();

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        store.Dispatch(new Load(3));
        store.Dispatch(new Loaded(1));
        store.Dispatch(new Loaded(2));
        store.Dispatch(first);
        store.Dispatch(new Derived());

        runs.Select(r => r.Action).ShouldBe(
            [new Load(1), new Load(2), new Load(3), new Loaded(1), new Loaded(2), first, new Derived()]);
        runs.Select(r => r.Token.IsCancellationRequested).ShouldBe([true, false, false, false, false, true, false]);
        runs[3].Token.ShouldBe(store.Dispatcher.Lifetime);
        runs[4].Token.ShouldBe(store.Dispatcher.Lifetime);
        store.Dispatcher.SlotCount.ShouldBe(3);

        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);

        async Task Record<TAction>(TAction action, EffectContext context, CancellationToken token)
            where TAction : notnull
        {
            runs.Add((action, token));
            await gate.Task.ConfigureAwait(false);
        }
    }

    // Non-normative: a duplicate On<T> throws DUCKY308 from the constructor, which the store's first use reports as
    // DUCKY353 naming DUCKY308 and wrapping it.
    [Fact]
    public async Task EffectGroup_DuplicateOn_Ducky353NamesDucky308()
    {
        var thrown = await FirstUseThrowsAsync<DuplicateGroup>();

        thrown.Message.ShouldContain($"The constructor of {typeof(DuplicateGroup)} threw {typeof(DuckyConfigurationException)} (DUCKY308)");
        thrown.InnerException.ShouldBeOfType<DuckyConfigurationException>().Errors
            .ShouldBe([DuckyErrors.DuplicateHandler(typeof(DuplicateGroup), typeof(Load))]);
    }

    // Non-normative: an interface or object handler type throws DUCKY307, reported as DUCKY353 naming DUCKY307.
    [Fact]
    public async Task EffectGroup_NonConcreteOn_Ducky353NamesDucky307()
    {
        var thrown = await FirstUseThrowsAsync<NonConcreteGroup>();

        thrown.Message.ShouldContain($"The constructor of {typeof(NonConcreteGroup)} threw {typeof(DuckyConfigurationException)} (DUCKY307)");
        thrown.InnerException.ShouldBeOfType<DuckyConfigurationException>().Errors
            .ShouldBe([DuckyErrors.NonConcreteHandlerType(typeof(NonConcreteGroup), typeof(IDisposable))]);
        Should.Throw<DuckyConfigurationException>(() => new Group(g => g.Register<object>((_, _, _) => Task.CompletedTask)))
            .Errors.ShouldBe([DuckyErrors.NonConcreteHandlerType(typeof(Group), typeof(object))]);
    }

    // Non-normative: On<T> is constructor only. Once a store created the group's runners it throws, while another store
    // given the same AddEffect(instance) instance still gets its own runners: a Switch run in the second store, in the one
    // global slot, does not supersede the first store's run, as a shared runner (and so a shared slot) would.
    [Fact]
    public async Task EffectGroup_OnAfterStoreCreatedRunners_Throws()
    {
        var gate = new TaskCompletionSource();
        List<(int Id, CancellationToken Token)> runs = [];
        var group = new Group(g => g.Register<Load>(
            async (load, _, token) =>
            {
                runs.Add((load.Id, token));
                await gate.Task.ConfigureAwait(false);
            },
            Concurrency.Switch));
        var first = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(group, false)]);
        var second = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(group, false)]);
        first.Dispatch(new Load(1));

        Should.Throw<InvalidOperationException>(() => group.Register<Loaded>((_, _, _) => Task.CompletedTask))
            .Message.ShouldBe($"{typeof(Group)} calls On<{typeof(Loaded)}>() after a store created its runners. Call On<T>() only from the constructor.");
        Should.Throw<ArgumentNullException>(() => new Group(g => g.Register<Load>(null!))).ParamName.ShouldBe("handler");

        second.Dispatch(new Load(2));
        runs.Select(r => r.Id).ShouldBe([1, 2]);
        runs.Select(r => r.Token.IsCancellationRequested).ShouldBe([false, false]);
        first.Dispatcher.SlotCount.ShouldBe(1);
        second.Dispatcher.SlotCount.ShouldBe(1);

        gate.SetResult();
        await first.WhenIdleAsync(TestContext.Current.CancellationToken);
        await second.WhenIdleAsync(TestContext.Current.CancellationToken);
        first.Dispatcher.SlotCount.ShouldBe(0);
        second.Dispatcher.SlotCount.ShouldBe(0);
    }

    private static async Task<DuckyConfigurationException> FirstUseThrowsAsync<TGroup>()
        where TGroup : EffectGroup
    {
        var services = new ServiceCollection().AddDucky(d =>
        {
            d.AddSlice<SeenSlice>();
            d.AddEffect<TGroup>();
        });
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        var thrown = Should.Throw<DuckyConfigurationException>(() => store.Dispatch(new Load(1)));

        thrown.Errors.ShouldHaveSingleItem().Code.ShouldBe("DUCKY353");
        return thrown;
    }
}
