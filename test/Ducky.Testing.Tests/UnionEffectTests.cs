namespace Ducky.Testing.Tests;

// #345 acceptance 8: EffectTest.Run takes a case of the union as the action, and the effect receives it wrapped in the union.
public sealed class UnionEffectTests
{
    [Fact]
    public async Task EffectTest_RunsACaseOfTheUnion()
    {
        var run = await EffectTest.Run(new LoadEffect(), new Filtered("milk"));

        run.Dispatched.ShouldBe([new Loaded("filtered milk")]);
    }

    internal sealed record Opened;

    internal sealed record Filtered(string Filter);

    internal sealed record Loaded(string What);

    internal union LoadTrigger(Opened, Filtered);

    internal sealed class LoadEffect : Effect<LoadTrigger>
    {
        public override Task Handle(LoadTrigger action, EffectContext context, CancellationToken cancellationToken)
        {
            context.Dispatch(new Loaded(action switch
            {
                Opened => "opened",
                Filtered filtered => "filtered " + filtered.Filter,
            }));
            return Task.CompletedTask;
        }

        // Generators are off in this project, so the cases are registered by hand.
        protected override void RegisterCases()
        {
            OnCase<Opened>(a => new LoadTrigger(a));
            OnCase<Filtered>(a => new LoadTrigger(a));
        }
    }
}
