using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests.Core;

#region getting-started-slice
public sealed record Greeting(string Name);

[DuckyAction]
public sealed record Rename(string Name);

public sealed class GreetingSlice : Slice<Greeting>
{
    public GreetingSlice() => On<Rename>((state, action) => state with { Name = action.Name });

    protected override Greeting Initial => new("world");
}
#endregion

// The snippets of docs/guides/core/getting-started.md.
public sealed class GettingStartedTests
{
    // Non-normative (M15-02): the generated registration and dispatch helpers work as the page says.
    [Fact]
    public async Task GettingStarted_GeneratedHelpers_ReduceInOrder()
    {
        var services = new ServiceCollection();

        #region getting-started-register
        services.AddDucky(ducky => ducky.AddDuckyGenerated_Ducky_Docs_Tests()); // every slice and effect of this assembly
        #endregion

        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        var initial = store.State.Get<Greeting>().Name;

        #region getting-started-dispatch
        store.Rename("Ada");                            // generated: store.Dispatch(new Rename("Ada"))
        var result = await store.RenameAsync("Grace"); // completes once the action is reduced
        var name = store.State.Get<Greeting>().Name;    // "Grace"
        #endregion

        initial.ShouldBe("world");
        result.ShouldBe(DispatchResult.Reduced);
        name.ShouldBe("Grace");
        store.Slices.ShouldContain(slice => slice is GreetingSlice);
    }

    // Non-normative (M15-02): a synchronous Dispatch on a caller that became the drainer reduces before it returns.
    [Fact]
    public async Task GettingStarted_SyncDispatchOnIdleStore_ReducedBeforeReturn()
    {
        await using var provider = new ServiceCollection()
            .AddDucky(ducky => ducky.AddDuckyGenerated_Ducky_Docs_Tests())
            .BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        store.Rename("Ada");

        store.State.Get<Greeting>().Name.ShouldBe("Ada");
    }
}
