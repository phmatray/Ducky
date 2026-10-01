using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// The §17.3 shape shared by every test here: DUCKY_REPEAT repetitions (default 50; the mutation targets set 1), and a
// 10 s WaitAsync bound that fails with a thread dump, so a synchronous deadlock (the INV-05 regression) fails the run
// instead of hanging it.
internal static class Interleaving
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    public static TheoryData<int> Repeat() =>
        [.. Enumerable.Range(1, int.TryParse(Environment.GetEnvironmentVariable("DUCKY_REPEAT"), out var n) ? n : 50)];

    public static DuckyStore Store(params Slice[] slices) => new(slices, NullLogger.Instance);

    public static async Task<T> Within<T>(Task<T> choreography)
    {
        await Within((Task)choreography);
        return await choreography;
    }

    public static Task Within(Task choreography) => Within(choreography, _bound);

    // A whole CsCheck run: 10 s as above at CsCheck's default iterations. PropertyLong's CsCheck_Iter (§19) lengthens the run
    // past any 10 s bound, so it gets a fixed hour, well inside the nightly job's 180 minutes; there each iteration still
    // blocks through Wait, so a deadlock fails with a thread dump in 10 s.
    public static Task WithinProperty(Task choreography) =>
        Within(choreography, Environment.GetEnvironmentVariable("CsCheck_Iter") is { Length: > 0 } ? TimeSpan.FromHours(1) : _bound);

    // The synchronous Within, for a step inside a CsCheck operation or sample (both synchronous).
    public static void Wait(Task step) => Within(step).GetAwaiter().GetResult();

    public static T Wait<T>(Task<T> step) => Within(step).GetAwaiter().GetResult();

    private static async Task Within(Task choreography, TimeSpan bound)
    {
        try
        {
            await choreography.WaitAsync(bound, TimeProvider.System, TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            Assert.Fail(ThreadDump(choreography, bound));
        }
    }

    // Every producer has returned, so nothing enqueues any more: the last drain's exit is the moment the queue is empty,
    // and INV-03 says no action is left in it.
    public static Task Settled(DuckyStore store) => Within(store.Dispatcher.DrainExited ?? Task.CompletedTask);

    // A managed stack of another thread needs a debugger (ClrMD, dotnet-stack); the OS thread states and the pool
    // counters are what the process can report about itself.
    private static string ThreadDump(Task choreography, TimeSpan bound)
    {
        using var process = Process.GetCurrentProcess();
        return string.Join(
            Environment.NewLine,
            [
                $"Choreography still {choreography.Status} after {bound.TotalSeconds} s: a deadlock or a stranded action.",
                $"Thread pool: {ThreadPool.ThreadCount} threads, {ThreadPool.PendingWorkItemCount} pending, {ThreadPool.CompletedWorkItemCount} completed work items.",
                .. process.Threads.Cast<ProcessThread>().Select(t => $"  OS thread {t.Id}: {Describe(t)}"),
            ]);
    }

    private static string Describe(ProcessThread thread)
    {
        try
        {
            return thread.ThreadState == System.Diagnostics.ThreadState.Wait
                ? $"Wait ({thread.WaitReason})"
                : thread.ThreadState.ToString();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or InvalidOperationException or NotSupportedException)
        {
            return $"state unavailable ({ex.GetType().Name})";
        }
    }
}
