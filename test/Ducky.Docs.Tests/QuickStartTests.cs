using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests;

#region quick-start-slice
public sealed record Counter(int Value);

public sealed record Increment(int By);

public sealed class CounterSlice : Slice<Counter>
{
    public CounterSlice() => On<Increment>((state, action) => state with { Value = state.Value + action.By });

    protected override Counter Initial => new(0);
}
#endregion

// The snippets of docs/index.md.
public sealed class QuickStartTests
{
    // Non-normative (M15-01): the quick start reduces as the page says.
    [Fact]
    public async Task QuickStart_DispatchIncrement_CounterReduced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        #region quick-start-dispatch
        await using var services = new ServiceCollection()
            .AddDucky(d => d.AddDuckyGenerated_Ducky_Docs_Tests()) // generated: every slice and effect of this assembly
            .BuildServiceProvider();
        var store = services.GetRequiredService<IStore>();
        await store.InitializeAsync(cancellationToken);

        var result = await store.DispatchAsync(new Increment(2));

        var count = store.State.Get<Counter>().Value; // 2
        #endregion

        result.ShouldBe(DispatchResult.Reduced);
        count.ShouldBe(2);
    }
}
