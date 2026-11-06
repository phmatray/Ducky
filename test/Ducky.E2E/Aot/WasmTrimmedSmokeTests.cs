using System.Text.Json;
using Ducky.E2E.Samples;
using Microsoft.Playwright;

namespace Ducky.E2E.Aot;

// INV-24, SPEC §10 (Smoke gates), §19 (AotSmoke): Ducky.Samples.Wasm published trimmed (TrimMode=full) with AOT, served
// from its wwwroot. The AotSmoke target sets DUCKY_REQUIRE_TRIMMED_PUBLISH and DUCKY_TRIMMED_PUBLISH_DIR, so a publish that
// landed elsewhere fails here; only a run without DUCKY_REQUIRE_TRIMMED_PUBLISH (the plain E2E target) skips.
public sealed class WasmTrimmedSmokeTests
{
    private const string Title = "Trimmed duck";
    // The AOT runtime's first load, on a 2-vCPU runner.
    private const float RenderTimeoutMs = 60_000;

    [Fact]
    public async Task WasmTrimmedSmoke()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable("DUCKY_REQUIRE_TRIMMED_PUBLISH") is not { Length: > 0 },
            "DUCKY_REQUIRE_TRIMMED_PUBLISH is unset: the trimmed AOT-WASM publish exists only in the AotSmoke target");
        var root = Environment.GetEnvironmentVariable("DUCKY_TRIMMED_PUBLISH_DIR");
        (root is { Length: > 0 } && File.Exists(Path.Combine(root, "index.html"))).ShouldBeTrue(
            $"DUCKY_TRIMMED_PUBLISH_DIR ('{root}') holds no index.html: the trimmed publish is missing or landed elsewhere");

        await using var harness = await Harness.StartAsync(root);
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await page.AddInitScriptAsync("""
            window.devtools = [];
            window.__REDUX_DEVTOOLS_EXTENSION__ = {
                connect() {
                    return {
                        init: () => {},
                        send: (action, state) => devtools.push({ action, state }),
                        subscribe: () => {},
                        unsubscribe: () => {},
                    };
                },
            };
            """);
        // ?devtools switches the static publish to Development, where Program.cs enables DevTools with Trace.
        await page.GotoAsync("/?devtools");
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Todos", new() { Timeout = RenderTimeoutMs });

        await page.GetByTestId("new-todo").FillAsync(Title);
        await page.GetByTestId("add").ClickAsync();

        await Assertions.Expect(page.GetByTestId("todo")).ToContainTextAsync(Title);
        await page.WaitForFunctionAsync("() => devtools.some(m => m.action.type === 'todos/added')", null, new() { PollingInterval = 50 });
        var added = JsonDocument.Parse(await page.EvaluateAsync<string>("() => JSON.stringify(devtools.find(m => m.action.type === 'todos/added').action)")).RootElement;
        added.GetProperty("payload").GetRawText().ShouldContain(Title);
        // Trace is on when the action carries a "stack" key (DevToolsMiddleware adds it only under Trace): that key is the
        // assertion. The trimmed AOT publish captures no frame (Mono walks no AOT-compiled WebAssembly frame; measured on
        // 10.0.14), so the stack is empty there and the frame-format check below is vacuous on 10.0.14 (SPEC §10). It bites
        // once a runtime captures frames: each one is "    at <type>.<name>", or "    at <unknown>" for trimmed metadata.
        added.TryGetProperty("stack", out var trace).ShouldBeTrue($"Trace attaches no stack: {added.GetRawText()}");
        trace.ValueKind.ShouldBe(JsonValueKind.String);
        trace.GetString()!.ShouldMatch(@"^(?:    at [^\n]+(?:\n    at [^\n]+)*)?\z");

        // The persisted slice is written to localStorage, then restored by a fresh load of the page.
        await page.WaitForFunctionAsync($"() => Object.values(localStorage).some(v => v.includes('{Title}'))", null, new() { PollingInterval = 50 });
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByTestId("todo")).ToContainTextAsync(Title, new() { Timeout = RenderTimeoutMs });
        errors.ShouldBeEmpty();
    }
}
