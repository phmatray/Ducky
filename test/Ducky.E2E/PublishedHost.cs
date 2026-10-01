using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Ducky.E2E;

// SPEC §17.2: runs `dotnet <published dll> --urls http://127.0.0.1:0`, reads the bound address from stdout, polls a
// health endpoint and kills the process tree on dispose; a host that exits or never answers fails with the last lines it
// printed. The E2E target publishes each sample of Ducky.slnx to $DUCKY_SAMPLES_DIR/<name>/ (§19); samples and the specs
// that start them arrive at stage 18.
internal sealed partial class PublishedHost : IAsyncDisposable
{
    private const int TailLines = 20;
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);
    private readonly Process _process;

    private PublishedHost(Process process, Uri baseUrl)
    {
        _process = process;
        BaseUrl = baseUrl;
    }

    public Uri BaseUrl { get; }

    public static Task<PublishedHost> StartSampleAsync(string sample, string healthPath = "/") =>
        StartAsync(Path.Combine(
            Environment.GetEnvironmentVariable("DUCKY_SAMPLES_DIR") ?? throw new InvalidOperationException("DUCKY_SAMPLES_DIR is not set: run the E2E target"),
            sample, $"{sample}.dll"), healthPath);

    public static async Task<PublishedHost> StartAsync(string dll, string healthPath = "/")
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(dll)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(dll);
        info.ArgumentList.Add("--urls");
        info.ArgumentList.Add("http://127.0.0.1:0");
        var bound = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tail = new ConcurrentQueue<string>();
        var process = new Process { StartInfo = info };
        // Both pipes are drained for the host's whole life, so a chatty host never blocks on a full pipe; their last lines
        // go into every startup failure.
        void OnLine(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                return;
            }
            tail.Enqueue(e.Data);
            if (tail.Count > TailLines)
            {
                tail.TryDequeue(out _);
            }
            if (ListeningOn().Match(e.Data) is { Success: true } match)
            {
                bound.TrySetResult(new Uri(match.Groups[1].Value));
            }
        }
        process.OutputDataReceived += OnLine;
        process.ErrorDataReceived += OnLine;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        // Completes once the process has exited and both pipes are drained, so the tail is whole when it is read.
        var exited = process.WaitForExitAsync();
        string Failure(string what) => $"{dll} {what}; its last output:{Environment.NewLine}{string.Join(Environment.NewLine, tail)}";
        using var timeout = new CancellationTokenSource(_startTimeout);
        try
        {
            if (await Task.WhenAny(bound.Task, exited).WaitAsync(timeout.Token) == exited && !bound.Task.IsCompleted)
            {
                throw new InvalidOperationException(Failure($"exited with code {process.ExitCode} before listening"));
            }
            var baseUrl = await bound.Task;
            using var http = new HttpClient { BaseAddress = baseUrl };
            while (!(await TryGetAsync(http, healthPath, timeout.Token)))
            {
                if (exited.IsCompleted)
                {
                    throw new InvalidOperationException(Failure($"exited with code {process.ExitCode} after listening on {baseUrl}"));
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, timeout.Token);
            }
            return new PublishedHost(process, baseUrl);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await StopAsync(process);
            throw new TimeoutException(Failure($"did not answer {healthPath} within {_startTimeout.TotalSeconds:0} s"));
        }
        catch
        {
            await StopAsync(process);
            throw;
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync(_process));

    private static async Task StopAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync();
        process.Dispose();
    }

    private static async Task<bool> TryGetAsync(HttpClient http, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"Now listening on: (http://\S+)")]
    private static partial Regex ListeningOn();
}
