namespace Ducky.Testing.Tests;

// EffectGroup<TAction> (ADR-0051, #314): one Handle over a union runs for every case, whether the case is dispatched alone
// or wrapped in the union (#304 unwraps it). Ducky.Testing.Tests runs without the generator, so the group registers its
// cases by hand, as the generator would.
public sealed class UnionEffectGroupTests
{
    [Fact]
    public async Task UnionEffectGroup_HandlesEachCase_DispatchedAloneOrInTheUnion()
    {
        await using var store = TestStore.Create(d =>
        {
            d.ThrowOnUnhandledAction = false; // Handled is a result, with no reducer
            d.AddEffect<CartEffects>();
        });

        await store.DispatchAsync(new AddItem("apple"));
        await store.DispatchAsync(new CartAction(new RemoveItem("pear")));
        await store.DispatchAsync(new Clear());
        await store.Settled();

        store.Processed.Select(p => p.Action).OfType<Handled>().ShouldBe([new Handled("add apple"), new Handled("remove pear"), new Handled("clear")]);
    }

    [Fact]
    public async Task UnionEffectGroup_WithoutRegisteredCases_HandlesNothing()
    {
        var run = await EffectTest.Run(new UnregisteredEffects(), new AddItem("apple"));

        run.Dispatched.ShouldBeEmpty();
    }

    internal sealed record AddItem(string Name);

    internal sealed record RemoveItem(string Name);

    internal sealed record Clear;

    internal sealed record Handled(string What);

    internal union CartAction(AddItem, RemoveItem, Clear);

    internal sealed class CartEffects : EffectGroup<CartAction>
    {
        public override Task Handle(CartAction action, EffectContext context, CancellationToken cancellationToken)
        {
            context.Dispatch(new Handled(action switch
            {
                AddItem a => "add " + a.Name,
                RemoveItem r => "remove " + r.Name,
                Clear => "clear",
            }));
            return Task.CompletedTask;
        }

        protected override void RegisterCases()
        {
            On<AddItem>((a, c, t) => Handle(new CartAction(a), c, t));
            On<RemoveItem>((a, c, t) => Handle(new CartAction(a), c, t));
            On<Clear>((a, c, t) => Handle(new CartAction(a), c, t));
        }
    }

    internal sealed class UnregisteredEffects : EffectGroup<CartAction>
    {
        public override Task Handle(CartAction action, EffectContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
