namespace Ducky.E2E;

// The harness's own guarantees (SPEC §17.1 Time, §17.2), which the later specs rely on without checking them.
public sealed class HarnessTests
{
    [Fact]
    public async Task Harness_SecondTabOfContext_KeepsTheFakeClock()
    {
        await using var harness = await Harness.StartAsync();
        var first = await harness.NewPageAsync();
        await first.GotoAsync("index.html");
        await first.Clock.RunForAsync(3_600_000);

        var second = await harness.NewPageAsync(first.Context);
        await second.GotoAsync("index.html");

        // An hour of fake time ran; a second install would put the context's clock back to wall-clock now.
        var later = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeMilliseconds();
        (await first.EvaluateAsync<double>("Date.now()")).ShouldBeGreaterThan(later);
        (await second.EvaluateAsync<double>("Date.now()")).ShouldBeGreaterThan(later);
    }

    [Fact]
    public async Task PublishedHost_HostExitsBeforeListening_ReportsItsOutput()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Missing.dll");

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => PublishedHost.StartAsync(dll));

        failure.Message.ShouldContain("before listening");
        failure.Message.ShouldContain("Could not execute because the specified command or file was not found", Case.Sensitive,
            "the muxer's own error output must be in the message");
    }
}
