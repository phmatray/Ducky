using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace DuckShop.Tests.Steps.Step1;

// Program.cs's registration as step 1 leaves it, before the app has a slice. The app's
// Program.cs has moved on (step 8 persists the basket), so step 1's version lives here.
internal static class Step1Program
{
    public static void Register(ProgramBuilder builder)
    {
        #region step1-register
        builder.Services.AddDucky(ducky => ducky
            .AddDuckyGenerated_DuckShop() // every slice and effect of this app
            .AddBlazor());                // Ducky's Blazor services
        #endregion
    }
}

// Stands in for Program.cs's WebAssemblyHostBuilder, which needs a browser: the same
// Services collection.
internal sealed class ProgramBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();
}
