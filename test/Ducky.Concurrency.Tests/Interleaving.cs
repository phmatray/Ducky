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

    public static async Task Within(Task choreography)
    {
        try
        {
            await choreography.WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);
        }
        catch (TimeoutException)
        {
            Assert.Fail(ThreadDump(choreography));
        }
    }

    // Every producer has returned, so nothing enqueues any more: the last drain's exit is the moment the queue is empty,
    // and INV-03 says no action is left in it.
    public static Task Settled(DuckyStore store) => Within(store.Dispatcher.DrainExited ?? Task.CompletedTask);

    // A managed stack of another thread needs a debugger (ClrMD, dotnet-stack); the OS thread states and the pool
    // counters are what the process can report about itself.
    private static string ThreadDump(Task choreography)
    {
        using var process = Process.GetCurrentProcess();
        return string.Join(
            Environment.NewLine,
            [
                $"Choreography still {choreography.Status} after {_bound.TotalSeconds} s: a deadlock or a stranded action.",
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
