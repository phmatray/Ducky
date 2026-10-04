namespace Ducky.E2E;

// The planted check of the JS block-coverage gate (M0-09), on real V8 output: ducky.js plus one block that
// never runs, served as planted.js (so the E2E target's gate never sees it), must fail on exactly that block.
public sealed class JsBlockCoverageTests
{
    private const string PlantedBlock = """

        export function planted(flag) {
            if (flag) {
                return 'planted';
            }
            return 'stub';
        }
        """;

    [Fact]
    public async Task JsBlockCoverage_PlantedUnexecutedBlock_FailsGate()
    {
        Assert.SkipUnless(Harness.MeasuresCoverage, "block coverage is measured in Chromium only (SPEC §17.1)");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var duckyJs = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "harness", "ducky.js"), TestContext.Current.CancellationToken);
        var planted = duckyJs + PlantedBlock;
        await page.RouteAsync("**/planted.js", route => route.FulfillAsync(new() { Body = planted, ContentType = "text/javascript" }));

        await page.GotoAsync("index.html");
        var result = await page.EvaluateAsync<string>("""
            async () => {
                const module = await import('/planted.js');
                return module.planted(false);
            }
            """);

        result.ShouldBe("stub");
        // Only the appended part is asserted, so the check holds whatever ducky.js itself holds.
        var zero = JsBlockCoverage.ZeroBlocks(await harness.TakeCoverageAsync(url => url.EndsWith("/planted.js", StringComparison.Ordinal)))
            .Where(b => b.Start >= duckyJs.Length)
            .ToList();
        var block = planted[zero.ShouldHaveSingleItem().Start..zero[0].End];
        block.ShouldContain("return 'planted'");
        block.ShouldNotContain("return 'stub'");
    }

    [Theory]
    [InlineData("http://127.0.0.1:5000/ducky.js", true)]
    [InlineData("https://localhost/_content/Ducky.Blazor/ducky.js?v=1", true)]
    [InlineData("https://localhost/_content/Ducky.Blazor/ducky.k3x9w2b7qa.js", true)]
    [InlineData("https://localhost/planted.js", false)]
    [InlineData("https://localhost/notducky.js", false)]
    [InlineData("https://localhost/ducky.js.map", false)]
    public void JsBlockCoverage_ServedUrl_IsKeyedToDuckyJs(string url, bool expected) =>
        JsBlockCoverage.IsDuckyJs(url).ShouldBe(expected);
}
