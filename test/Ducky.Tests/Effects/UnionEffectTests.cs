using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

/// <summary>One <c>Effect&lt;TUnion&gt;</c> shares one concurrency slot table across every case of the union (#345).</summary>
public sealed class UnionEffectTests
{
    [Fact]
    public async Task Switch_ACaseSupersedesARunStartedByAnotherCase()
    {
        var gate = new TaskCompletionSource();
        var firstToken = CancellationToken.None;
        var effect = new TriggerEffect(
            async (trigger, context, token) =>
            {
                if (trigger.Value is Refreshed)
                {
                    context.Dispatch(new Loaded(2));
                    return;
                }

                firstToken = token;
                await gate.Task.ConfigureAwait(false);
                context.Dispatch(new Loaded(1));
            },
            Concurrency.Switch);
        await using var store = Build(effect);

        store.Dispatch(new Opened(1));
        store.Dispatcher.SlotCount.ShouldBe(1); // one slot, however many cases index the runner
        firstToken.IsCancellationRequested.ShouldBeFalse();

        store.Dispatch(new Refreshed(1));
        firstToken.IsCancellationRequested.ShouldBeTrue();

        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Loaded(2)]);
    }

    [Fact]
    public async Task Exhaust_ACaseIsDroppedWhileAnotherCaseRuns()
    {
        var gate = new TaskCompletionSource();
        List<object?> handled = [];
        var effect = new TriggerEffect(
            async (trigger, _, _) =>
            {
                handled.Add(trigger.Value);
                await gate.Task.ConfigureAwait(false);
            },
            Concurrency.Exhaust);
        await using var store = Build(effect);

        store.Dispatch(new Opened(1));
        store.Dispatch(new Refreshed(1));
        handled.ShouldBe([new Opened(1)]);

        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        store.Dispatch(new Left(1)); // the slot is free again, for any case
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        handled.ShouldBe([new Opened(1), new Left(1)]);
    }

    [Fact]
    public async Task ConcurrencyKey_RelatesRunsAcrossCases()
    {
        var gate = new TaskCompletionSource();
        List<CancellationToken> tokens = [];
        var effect = new TriggerEffect(
            async (_, _, token) =>
            {
                tokens.Add(token);
                await gate.Task.ConfigureAwait(false);
            },
            Concurrency.Switch,
            trigger => trigger switch
            {
                Opened opened => opened.Id,
                Refreshed refreshed => refreshed.Id,
                Left left => left.Id,
            });
        await using var store = Build(effect);

        store.Dispatch(new Opened(1));
        store.Dispatch(new Refreshed(2)); // a different key runs side by side
        tokens.Select(t => t.IsCancellationRequested).ShouldBe([false, false]);

        store.Dispatch(new Left(1)); // an equal key shares the slot, across cases
        tokens.Select(t => t.IsCancellationRequested).ShouldBe([true, false, false]);

        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CaseAloneOrInsideTheUnion_RunsHandleWrappedInTheUnion()
    {
        List<LoadTrigger> handled = [];
        var effect = new TriggerEffect((trigger, _, _) =>
        {
            handled.Add(trigger);
            return Task.CompletedTask;
        });
        await using var store = Build(effect);

        store.Dispatch(new Opened(1));
        store.Dispatch(new LoadTrigger(new Refreshed(2)));
        store.Dispatch(new OuterTrigger(new LoadTrigger(new Left(3))));
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        handled.Select(t => t.Value).ShouldBe([new Opened(1), new Refreshed(2), new Left(3)]);
    }

    [Fact]
    public async Task CaseInTwoTriggerUnions_StartsBothEffects()
    {
        var gate = new TaskCompletionSource();
        List<CancellationToken> loads = [];
        List<CancellationToken> audits = [];
        var load = new TriggerEffect(
            async (_, _, token) =>
            {
                loads.Add(token);
                await gate.Task.ConfigureAwait(false);
            },
            Concurrency.Switch);
        var audit = new AuditEffect(async token =>
        {
            audits.Add(token);
            await gate.Task.ConfigureAwait(false);
        });
        await using var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(load, false), (audit, false)]);

        store.Dispatch(new Opened(1));
        loads.Count.ShouldBe(1);
        audits.Count.ShouldBe(1);

        store.Dispatch(new Refreshed(1)); // only LoadTrigger has this case: each effect keeps its own slot table
        loads[0].IsCancellationRequested.ShouldBeTrue();
        audits[0].IsCancellationRequested.ShouldBeFalse();

        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoRegisteredCase_HandlesNothing()
    {
        var effect = new CaseRegistrar();
        await using var store = Build(effect);

        store.Dispatch(new Opened(1));
        store.Dispatch(new LoadTrigger(new Refreshed(2)));
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        effect.Handled.ShouldBe(0);
    }

    [Fact]
    public async Task OnCase_RejectsWhatOnRejects()
    {
        Should.Throw<ArgumentNullException>(() => new CaseRegistrar(e => e.Register<Opened>(null!))).ParamName.ShouldBe("wrap");

        CodeOf(e => e.Register<LoadTrigger>(a => a)).ShouldBe("DUCKY319");
        CodeOf(e => e.Register<AbstractCase>(_ => default)).ShouldBe("DUCKY307");
        CodeOf(e => e.Register<IMarkerCase>(_ => default)).ShouldBe("DUCKY307");
        CodeOf(e => e.Register<object>(_ => default)).ShouldBe("DUCKY307");
        CodeOf(e =>
        {
            e.Register<Opened>(a => new LoadTrigger(a));
            e.Register<Opened>(a => new LoadTrigger(a));
        }).ShouldBe("DUCKY308");

        var effect = new CaseRegistrar(e => e.Register<Opened>(a => new LoadTrigger(a)));
        await using var store = Build(effect);
        store.Dispatch(new Opened(1));

        Should.Throw<InvalidOperationException>(() => effect.Register<Refreshed>(a => new LoadTrigger(a)))
            .Message.ShouldBe($"{typeof(CaseRegistrar)} calls OnCase<{typeof(Refreshed)}>() after a store created its runners. Call OnCase<T>() only from RegisterCases().");
    }

    [Fact]
    public void OnCase_ErrorsNameOnCase()
    {
        MessageOf(e => e.Register<LoadTrigger>(a => a)).ShouldContain("calls OnCase<");
        MessageOf(e => e.Register<AbstractCase>(_ => default)).ShouldContain("calls OnCase<");
        MessageOf(e =>
        {
            e.Register<Opened>(a => new LoadTrigger(a));
            e.Register<Opened>(a => new LoadTrigger(a));
        }).ShouldContain("calls OnCase<");
    }

    [Fact]
    public void OnCase_OnAnOrdinaryEffect_Throws() =>
        Should.Throw<InvalidOperationException>(() => new PlainRegistrar())
            .Message.ShouldBe($"{typeof(PlainRegistrar)} calls OnCase<{typeof(Refreshed)}>(), but {typeof(Opened)} is not a C# union: an ordinary effect handles its action type itself.");

    private static string MessageOf(Action<CaseRegistrar> register) =>
        Should.Throw<DuckyConfigurationException>(() => new CaseRegistrar(register)).Errors.ShouldHaveSingleItem().Message;

    private static string CodeOf(Action<CaseRegistrar> register) =>
        Should.Throw<DuckyConfigurationException>(() => new CaseRegistrar(register)).Errors.ShouldHaveSingleItem().Code;

    private static DuckyStore Build(Effect effect) =>
        new([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

    internal sealed record Opened(int Id);

    internal sealed record Refreshed(int Id);

    internal sealed record Left(int Id);

    internal sealed record Audited(int Id);

    internal abstract record AbstractCase;

    internal interface IMarkerCase;

    internal union LoadTrigger(Opened, Refreshed, Left);

    internal union AuditTrigger(Opened, Audited);

    internal union OuterTrigger(LoadTrigger, Audited);

    private sealed class TriggerEffect(
        Func<LoadTrigger, EffectContext, CancellationToken, Task> handle,
        Concurrency policy = Concurrency.Merge,
        Func<LoadTrigger, object?>? key = null) : Effect<LoadTrigger>
    {
        public override Concurrency Policy => policy;

        public override Task Handle(LoadTrigger action, EffectContext context, CancellationToken cancellationToken) =>
            handle(action, context, cancellationToken);

        protected override object? ConcurrencyKey(LoadTrigger action) => key?.Invoke(action);

        protected override void RegisterCases()
        {
            OnCase<Opened>(a => new LoadTrigger(a));
            OnCase<Refreshed>(a => new LoadTrigger(a));
            OnCase<Left>(a => new LoadTrigger(a));
        }
    }

    private sealed class AuditEffect(Func<CancellationToken, Task> handle) : Effect<AuditTrigger>
    {
        public override Concurrency Policy => Concurrency.Switch;

        public override Task Handle(AuditTrigger action, EffectContext context, CancellationToken cancellationToken) =>
            handle(cancellationToken);

        protected override void RegisterCases()
        {
            OnCase<Opened>(a => new AuditTrigger(a));
            OnCase<Audited>(a => new AuditTrigger(a));
        }
    }

    private sealed class PlainRegistrar : Effect<Opened>
    {
        public override Task Handle(Opened action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override void RegisterCases() => OnCase<Refreshed>(a => new Opened(a.Id));
    }

    private sealed class CaseRegistrar(Action<CaseRegistrar>? register = null) : Effect<LoadTrigger>
    {
        public int Handled { get; private set; }

        public void Register<TCase>(Func<TCase, LoadTrigger> wrap)
            where TCase : notnull => OnCase(wrap);

        public override Task Handle(LoadTrigger action, EffectContext context, CancellationToken cancellationToken)
        {
            Handled++;
            return Task.CompletedTask;
        }

        protected override void RegisterCases() => register?.Invoke(this);
    }
}
