using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// The §17.3 shape shared by every test here: DUCKY_REPEAT repetitions (default 50; the mutation targets set 1), and a
// 10 s WaitAsync bound that fails with a thread dump, so a synchronous deadlock (the INV-05 regression) fails the run
// instead of hanging it.
internal static class Interleaving
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    // ponytail: one process-wide stamp, so WithinProperty assumes no test calls Wait concurrently with it: every class
    // calling Wait joins [Collection(nameof(Interleaving))], whose tests run one at a time; make it per run if that
    // serialization costs too much.
    private static long _lastStep;

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

    // A whole CsCheck run, bounded by progress, not by its length: it fails with a thread dump once _bound passes with no
    // step entering or leaving Wait. A blocking step is bounded by Wait itself; a hang inside a synchronous store call (the
    // inline drain in Dispatch or DispatchAsync, State, construction) stops the steps, so it fails within _bound too,
    // however long a live run takes (a cold 2-vCPU runner, PropertyLong's CsCheck_Iter). The gap is measured from the
    // stamp Wait wrote, so a starved pool that delays this poll never shortens it.
    public static async Task WithinProperty(Task run)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Step();
        while (await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(1), TimeProvider.System, cancellationToken)) != run)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TimeProvider.System.GetElapsedTime(Volatile.Read(ref _lastStep)) > _bound)
            {
                Assert.Fail(ThreadDump(run));
            }
        }

        await run;
    }

    // The synchronous Within, for a step inside a CsCheck operation or sample (both synchronous). Its entry and exit are
    // the progress WithinProperty watches.
    public static void Wait(Task step)
    {
        Step();
        Within(step).GetAwaiter().GetResult();
        Step();
    }

    public static T Wait<T>(Task<T> step)
    {
        Wait((Task)step);
        return step.GetAwaiter().GetResult();
    }

    private static void Step() => Volatile.Write(ref _lastStep, TimeProvider.System.GetTimestamp());

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
                $"Choreography still {choreography.Status} with no progress for {_bound.TotalSeconds} s: a deadlock or a stranded action.",
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
