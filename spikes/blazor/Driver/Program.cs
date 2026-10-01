using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

// Usage: dotnet run --project Driver -- <Host.dll> <server|wasm|auto>... [--port N]
// Starts the host once per mode (SPIKE_MODES), drives the pages and prints every answer.
var hostDll = Path.GetFullPath(args[0]);
var modes = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var port = 5300;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();
using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });

foreach (var mode in modes)
{
    var url = $"http://127.0.0.1:{++port}";
    Console.WriteLine($"\n######## mode {mode} ({url}, {hostDll})");
    using var host = Start(mode, url);
    try
    {
        await WaitUp(url);
        await Run(mode, url);
    }
    finally
    {
        host.Kill(entireProcessTree: true);
        await host.WaitForExitAsync();
    }
}

async Task Run(string mode, string url)
{
    // The prerender pass, from the raw HTML: its probe outcome and whether a seed was persisted, and where.
    var html = await http.GetStringAsync($"{url}/s7");
    Section("prerender pass (raw HTML of /s7)");
    Print(html, "mode", "probe", "seed");
    Console.WriteLine($"server state comment: {StateLength(html, "Server")}; webassembly state comment: {StateLength(html, "WebAssembly")}");

    var context = await browser.NewContextAsync();
    var page = await context.NewPageAsync();
    page.Console += (_, m) => { if (m.Type is "error" or "warning") { Console.WriteLine($"[console {m.Type}] {m.Text}"); } };
    await page.GotoAsync($"{url}/login?user=alice");
    await Ready(page);
    Section("interactive /s7 after login (first visit)");
    await Dump(page);

    if (mode == "server")
    {
        Section("pause/resume after three increments");
        await page.ClickAsync("#inc");
        await page.ClickAsync("#inc");
        await page.ClickAsync("#inc");
        await WaitFor(() => page.InnerTextAsync("#inc"), t => t.Contains("counter 3", StringComparison.Ordinal), 10);
        var storeBefore = await page.InnerTextAsync("#mode");
        Console.WriteLine($"Blazor.pauseCircuit() = {await page.EvaluateAsync<bool>("Blazor.pauseCircuit()")}");
        if (!await WaitFor(() => page.InnerTextAsync("#mode"), t => t != storeBefore, 5))
        {
            try
            {
                Console.WriteLine($"Blazor.resumeCircuit() = {await page.EvaluateAsync<bool>("Blazor.resumeCircuit()")}");
            }
            catch (PlaywrightException e)
            {
                Console.WriteLine($"Blazor.resumeCircuit() threw: {e.Message.Split('\n')[0]}");
            }

            await WaitFor(() => page.InnerTextAsync("#mode"), t => t != storeBefore, 15);
        }
        else
        {
            Console.WriteLine("resumed without a resumeCircuit() call (reconnection handler)");
        }

        await Dump(page);

        Section("reconnect: Blazor._internal.forceCloseConnection()");
        var before = (await page.InnerTextAsync("#auth")).Split('\n').Length;
        await page.EvaluateAsync("Blazor._internal.forceCloseConnection()");
        if (!await WaitFor(() => page.InnerTextAsync("#auth"), t => t.Split('\n').Length > before, 15))
        {
            Console.WriteLine("no new auth line within 15 s; calling Blazor.reconnect()");
            Console.WriteLine($"Blazor.reconnect() = {await page.EvaluateAsync<bool>("Blazor.reconnect()")}");
            await WaitFor(() => page.InnerTextAsync("#auth"), t => t.Split('\n').Length > before, 10);
        }

        await page.ClickAsync("#inc");
        await Dump(page);

        Section("pause/resume again, on the reconnected circuit");
        storeBefore = await page.InnerTextAsync("#mode");
        try
        {
            Console.WriteLine($"Blazor.pauseCircuit() = {await page.EvaluateAsync<bool>("Blazor.pauseCircuit()")}");
            Console.WriteLine($"Blazor.resumeCircuit() = {await page.EvaluateAsync<bool>("Blazor.resumeCircuit()")}");
        }
        catch (PlaywrightException e)
        {
            Console.WriteLine($"threw: {e.Message.Split('\n')[0]}");
        }

        Console.WriteLine($"new store: {await WaitFor(() => page.InnerTextAsync("#mode"), t => t != storeBefore, 15)}");
        await Dump(page);
    }

    if (mode == "auto")
    {
        Section("later visit on the WebAssembly side (resources cached)");
        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(2000);
            await page.ReloadAsync();
            await Ready(page);
            if ((await page.InnerTextAsync("#mode")).Contains("browser=True", StringComparison.Ordinal))
            {
                break;
            }
        }

        await Dump(page);
    }

    Section("S-5 /s5");
    var s5 = await context.NewPageAsync();
    await s5.GotoAsync($"{url}/s5");
    await Ready(s5);
    await s5.ClickAsync("#run");
    await s5.WaitForSelectorAsync("#s5-done", new() { Timeout = 120_000 });
    Console.WriteLine(await s5.InnerTextAsync("#s5"));
    await s5.ClickAsync("#ping");
    var alive = await WaitFor(() => s5.InnerTextAsync("#ping"), t => t.Contains("ping 1", StringComparison.Ordinal), 10);
    Console.WriteLine($"ping after the run re-rendered: {alive}; reconnect UI visible: {await ReconnectUiVisible(s5)}");

    Section("null-mode contrast: /s7?nullmode=1, then /s7 (raw HTML and interactive)");
    var response = await http.GetAsync($"{url}/s7?nullmode=1");
    var nullHtml = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"GET /s7?nullmode=1: {(int)response.StatusCode}");
    Print(nullHtml, "nullmode", "seed");
    Console.WriteLine($"server state comment: {StateLength(nullHtml, "Server")}; webassembly state comment: {StateLength(nullHtml, "WebAssembly")}");
    if (response.IsSuccessStatusCode)
    {
        var other = await context.NewPageAsync();
        await other.GotoAsync($"{url}/s7?nullmode=1");
        await Ready(other);
        Console.WriteLine($"interactive seed: {await other.InnerTextAsync("#seed")}");
        Console.WriteLine($"interactive nullmode: {await other.InnerTextAsync("#nullmode")}");
    }

    Section("/spike-log");
    Console.WriteLine(await http.GetStringAsync($"{url}/spike-log"));
    await context.CloseAsync();
}

Process Start(string mode, string url)
{
    var info = new ProcessStartInfo("dotnet", [hostDll])
    {
        WorkingDirectory = Path.GetDirectoryName(hostDll)!,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    info.Environment["SPIKE_MODES"] = mode;
    info.Environment["ASPNETCORE_URLS"] = url;
    info.Environment["ASPNETCORE_ENVIRONMENT"] = Environment.GetEnvironmentVariable("SPIKE_ENVIRONMENT") ?? "Development";
    Directory.CreateDirectory("out");
    var log = TextWriter.Synchronized(new StreamWriter($"out/host-{mode}.log") { AutoFlush = true });
    var process = Process.Start(info)!;
    process.OutputDataReceived += (_, e) => Log(e.Data);
    process.ErrorDataReceived += (_, e) => Log(e.Data);
    process.Exited += (_, _) => log.Dispose();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;

    void Log(string? line)
    {
        if (line is null)
        {
            return;
        }

        log.WriteLine(line);
        if (line.StartsWith("fail:", StringComparison.Ordinal))
        {
            Console.WriteLine($"[host] {line} (out/host-{mode}.log)");
        }
    }
}

async Task WaitUp(string url)
{
    for (var i = 0; i < 100; i++)
    {
        try
        {
            await http.GetAsync($"{url}/spike-log");
            return;
        }
        catch (HttpRequestException)
        {
            await Task.Delay(200);
        }
    }

    throw new TimeoutException("host did not start");
}

static async Task Ready(IPage page) => await page.WaitForSelectorAsync("#ready", new() { Timeout = 60_000 });

static async Task Dump(IPage page)
{
    foreach (var id in new[] { "mode", "identity", "probe", "seed", "nullmode", "lifetimes", "auth" })
    {
        var text = await page.InnerTextAsync($"#{id}");
        if (text.Length > 0)
        {
            Console.WriteLine($"{id}:\n  {text.Replace("\n", "\n  ", StringComparison.Ordinal)}");
        }
    }
}

static async Task<bool> WaitFor(Func<Task<string>> read, Func<string, bool> done, int seconds)
{
    var watch = Stopwatch.StartNew();
    while (watch.Elapsed < TimeSpan.FromSeconds(seconds))
    {
        try
        {
            if (done(await read()))
            {
                return true;
            }
        }
        catch (PlaywrightException)
        {
            // The page is between renders.
        }

        await Task.Delay(200);
    }

    return false;
}

static async Task<bool> ReconnectUiVisible(IPage page) =>
    await page.EvaluateAsync<bool>("() => { const e = document.getElementById('components-reconnect-modal'); return !!e && getComputedStyle(e).display !== 'none' && e.open !== false; }");

static void Section(string title) => Console.WriteLine($"\n--- {title}");

static void Print(string html, params string[] ids)
{
    foreach (var id in ids)
    {
        var match = Regex.Match(html, $"<pre id=\"{id}\">(.*?)</pre>", RegexOptions.Singleline);
        Console.WriteLine($"{id}: {(match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : "(absent)")}");
    }
}

static string StateLength(string html, string kind)
{
    var match = Regex.Match(html, $"<!--Blazor-{kind}-Component-State:(.*?)-->", RegexOptions.Singleline);
    return match.Success ? $"{match.Groups[1].Length} chars" : "absent";
}
