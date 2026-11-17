using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests;

// C# 15 unions as actions: Dispatch unwraps to the case, so exact-type matching (ADR-0008) still holds (#304).
public sealed class UnionActionTests
{
    [Fact]
    public async Task UnionAction_RunsTheCaseReducerAndEffect()
    {
        var effect = new CaseEffect();
        await using var store = Build(d => d.AddSlice<TotalSlice>().AddEffect(effect));

        await store.DispatchAsync(new Change(new Add(5)));
        await store.DispatchAsync(new Outer(new Change(new Add(2)))); // nested: unwraps until the case
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        store.State.Get<Total>().ShouldBe(new Total(7));
        effect.Seen.ShouldBe([5, 2]);
    }

    [Fact]
    public async Task DefaultUnion_FailsLikeNull()
    {
        await using var store = Build(d => d.AddSlice<TotalSlice>());

        Should.Throw<ArgumentNullException>(() => store.Dispatch(default(Change)));
        Should.Throw<ArgumentNullException>(() => store.DispatchAsync(default(Change)));
    }

    [Fact]
    public void HandlerOnUnionType_IsRejectedWithItsCases()
    {
        var slice = Should.Throw<DuckyConfigurationException>(() => new UnionSlice());
        slice.Errors.Single().Code.ShouldBe("DUCKY319");
        slice.Errors.Single().Message.ShouldContain("On<Ducky.Tests.UnionActionTests.Mode>()");

        Should.Throw<DuckyConfigurationException>(() => new UnionGroup()).Errors.Single().Code.ShouldBe("DUCKY319");
    }

    [Fact]
    public void UnionState_RoundTripsThroughJson()
    {
        using var provider = new ServiceCollection().AddDucky(d => d.AddSlice<ModeSlice>().UseJson(new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() })).BuildServiceProvider();
        var json = provider.GetRequiredService<IStore>().Json;
        var state = new Holder(3);

        json.TrySerialize(state, typeof(Holder), out var text).ShouldBeTrue();
        using var document = System.Text.Json.JsonDocument.Parse(text);
        json.TryDeserialize(document.RootElement, typeof(Holder), out var back).ShouldBeTrue();

        ((Holder)back!).Mode.Value.ShouldBe(3);
    }

    private static DuckyStore Build(Action<DuckyBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddDucky(configure);
        return (DuckyStore)services.BuildServiceProvider().GetRequiredService<IStore>();
    }

    internal sealed record Add(int Amount);

    internal sealed record Reset;

    internal union Change(Add, Reset);

    internal union Outer(Change, Other);

    internal sealed record Other;

    internal union Mode(int, string);

    internal sealed record Total(int Value);

    internal sealed class TotalSlice : Slice<Total>
    {
        public TotalSlice() => On<Add>((state, add) => new(state.Value + add.Amount));

        protected override Total Initial => new(0);
    }

    // Slice<TState> needs a reference type, so a union state lives in a record.
    internal sealed record Holder(Mode Mode);

    internal sealed class ModeSlice : Slice<Holder>
    {
        protected override Holder Initial => new("off");
    }

    internal sealed class UnionSlice : Slice<Total>
    {
        public UnionSlice() => On<Mode>(state => state);

        protected override Total Initial => new(0);
    }

    internal sealed class UnionGroup : EffectGroup
    {
        public UnionGroup() => On<Mode>((_, _, _) => Task.CompletedTask);
    }

    internal sealed class CaseEffect : Effect<Add>
    {
        public List<int> Seen { get; } = [];

        public override Task Handle(Add action, EffectContext context, CancellationToken cancellationToken)
        {
            Seen.Add(action.Amount);
            return Task.CompletedTask;
        }
    }
}
