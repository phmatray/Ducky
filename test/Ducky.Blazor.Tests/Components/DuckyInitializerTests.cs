using Bunit;
using Ducky.Blazor.Tests.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Tests.Components;

// SPEC §11.1, §6.7: DuckyInitializer awaits Store.InitializeAsync() in OnInitializedAsync.
public sealed class DuckyInitializerTests : BunitContext
{
    private readonly InitHold _hold = new();

    public DuckyInitializerTests()
    {
        Services.AddSingleton(_hold);
        Services.AddDucky(d => d.AddSlice<CounterSlice>().Use<HeldInit>());
    }

    [Fact]
    public async Task DuckyInitializer_ShowsLoadingUntilReady()
    {
        var cut = Render<DuckyInitializer>(p => p
            .Add(c => c.Loading, "loading")
            .Add(c => c.ChildContent, "ready"));
        cut.Markup.ShouldBe("loading");

        _hold.Released.SetResult();
        await Services.GetRequiredService<IStore>().InitializeAsync(Xunit.TestContext.Current.CancellationToken);
        await cut.InvokeAsync(() => { });

        cut.Markup.ShouldBe("ready");
    }

    [Fact]
    public void DuckyInitializer_InitAlreadyComplete_RendersChildContentFirst()
    {
        // (non-normative) With init already complete the first render shows ChildContent; Loading is optional.
        _hold.Released.SetResult();
        var cut = Render<DuckyInitializer>(p => p.Add(c => c.ChildContent, "ready"));

        cut.Markup.ShouldBe("ready");
        cut.RenderCount.ShouldBe(1);
    }
}
