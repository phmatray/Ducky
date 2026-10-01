namespace Ducky.E2E;

// Stage 1 (SPEC §24): the harness spec over the stub ducky.js; replaced by the real ducky.js specs (M6-01), which
// delete it and its manifest entry.
public sealed class SkeletonTests
{
    [Fact]
    public async Task Skeleton_DuckyE2E_Smoke()
    {
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();

        await page.GotoAsync("index.html");
        var ready = await page.EvaluateAsync<bool>("async () => (await import('/ducky.js')).ready()");

        ready.ShouldBeTrue();
    }
}
