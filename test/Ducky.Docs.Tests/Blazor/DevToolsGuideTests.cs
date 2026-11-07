using System.Text.Json.Nodes;
using Ducky.Blazor;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.JSInterop;

namespace Ducky.Docs.Tests.Blazor;

// The snippets of docs/guides/blazor/devtools.md.
public sealed class DevToolsGuideTests
{
    private static WebApplicationBuilder CreateBuilder(string[] args)
    {
        #region devtools-register
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDucky(ducky =>
        {
            ducky.UseJson(DocsJson.Default).AddSlice<BasketSlice>().AddBlazor();
            if (builder.Environment.IsDevelopment())
            {
                ducky.UseDevTools(options =>
                {
                    options.Name = "Shop";
                    // The basket is personal data: the extension sees a placeholder.
                    options.StateSanitizer = static (sliceKey, state) => sliceKey == "basket" ? JsonValue.Create("<redacted>") : state;
                });
            }
        });
        #endregion
        return builder;
    }

    // Non-normative (M15-03): in Development the store connects to the extension under its name and sends sanitized state;
    // in Production it never touches DevTools.
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task UseDevTools_OnlyInDevelopment_Sanitized(string environment)
    {
        var builder = CreateBuilder(["--environment", environment]);
        var browser = new FakeBrowser { Extension = true };
        builder.Services.AddSingleton<IJSRuntime>(browser);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.DispatchAsync(new AddedToBasket("duck-yellow"));

        var devTools = browser.Calls.Where(static c => c.Identifier.StartsWith("devtools", StringComparison.Ordinal)).ToList();
        if (environment == "Production")
        {
            devTools.ShouldBeEmpty();
            return;
        }

        // Connected under its name, then @@INIT, StoreInitialized and the AddedToBasket commit.
        devTools.Select(static c => c.Identifier).ShouldBe(["devtoolsConnect", "devtoolsInit", "devtoolsSend", "devtoolsSend"]);
        ((string)devTools[0].Args[2]!).ShouldContain("\"name\":\"Shop\"");
        JsonNode.Parse((string)devTools[3].Args[2]!)!["basket"]!.GetValue<string>().ShouldBe("<redacted>");
    }
}
