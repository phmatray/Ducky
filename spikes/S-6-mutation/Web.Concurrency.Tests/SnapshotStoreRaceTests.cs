using Microsoft.Extensions.Logging.Testing;
using Web;

namespace Web.Concurrency.Tests;

// The §17.3 shape: DUCKY_REPEAT repetitions (default 50), Barrier choreography, a 10 s WaitAsync bound. Each run appends
// a line to $S6_RUN_LOG, so the spike can count how often a mutant runs it.
public sealed class SnapshotStoreRaceTests
{
    public static TheoryData<int> Repeat() =>
        [.. Enumerable.Range(1, int.TryParse(Environment.GetEnvironmentVariable("DUCKY_REPEAT"), out var n) ? n : 50)];

    [Theory]
    [MemberData(nameof(Repeat))]
    public async Task Accept_TwoWritersSameVersion_ExactlyOneWins(int repeat)
    {
        if (Environment.GetEnvironmentVariable("S6_RUN_LOG") is { Length: > 0 } log)
        {
            var mutant = string.Join(' ', Environment.GetEnvironmentVariables().Keys.Cast<string>()
                .Where(k => k.Contains("STRYKER", StringComparison.OrdinalIgnoreCase)).Order()
                .Select(k => $"{k}={Environment.GetEnvironmentVariable(k)}"));
            await File.AppendAllTextAsync(log, $"pid={Environment.ProcessId} repeat={repeat} DUCKY_REPEAT={Environment.GetEnvironmentVariable("DUCKY_REPEAT")} {mutant}{Environment.NewLine}", TestContext.Current.CancellationToken);
        }

        var store = new SnapshotStore(new FakeLogger<SnapshotStore>());
        var cancellationToken = TestContext.Current.CancellationToken;
        using var barrier = new Barrier(2);
        var writers = Enumerable.Range(0, 2).Select(_ => Task.Run(
            () =>
            {
                barrier.SignalAndWait(cancellationToken);
                return store.Accept("""{"Key":"cart","Version":1}""");
            },
            cancellationToken));

        var results = await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, cancellationToken);

        results.Count(r => r).ShouldBe(1);
        store.Count.ShouldBe(1);
    }
}
