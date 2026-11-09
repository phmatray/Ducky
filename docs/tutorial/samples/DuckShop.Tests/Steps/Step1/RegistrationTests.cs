using Ducky;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace DuckShop.Tests.Steps.Step1;

public sealed class RegistrationTests
{
    // Step 1's registration builds a store; once the app declares slices, the generated
    // method registers every one of them.
    [Fact]
    public async Task Step1_Registration_BuildsAStoreWithEverySliceOfTheApp()
    {
        var builder = new ProgramBuilder();
        Step1Program.Register(builder);
        builder.Services
            .AddSingleton(TimeProvider.System)
            .AddSingleton<ICatalogueApi, InstantCatalogueApi>()
            .AddSingleton<ISearchApi, FakeSearchApi>()
            .AddSingleton<IJSRuntime, FakeBrowserStorage>();
        await using var provider = builder.Services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // The app's three slices, next to the ones Ducky adds (their keys start with "@ducky/").
        store.Slices.Select(slice => slice.Key).Where(key => !key.StartsWith("@ducky/", StringComparison.Ordinal))
            .ShouldBe(["basket", "catalogue", "search"], ignoreOrder: true);
    }
}
