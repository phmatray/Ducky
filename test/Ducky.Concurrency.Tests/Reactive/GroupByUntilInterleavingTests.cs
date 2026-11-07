using System.Collections.Concurrent;
using Ducky.Docs.Tests.Reactive;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace Ducky.Concurrency.Tests;

// The GroupByUntil of docs/guides/reactive/recipes.md (M15-03, non-normative), against the interleaving its keyed-switch
// recipe meets in production: a Debounce duration firing on a timer thread while the source pushes into the same group.
[Collection(nameof(Interleaving))]
public sealed class GroupByUntilInterleavingTests
{
    private static Task Dedicated(Action action, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    // Debounce emits under its own lock, and a push into the group enters that lock, so the expiry must never wait for a
    // push in progress: neither thread may block. The push parks in the group's subscriber (R3 calls it before the Debounce
    // subscribed after it) while the timer thread fires the debounce. The pushed item still reaches the group, which closes
    // after the push, not during it, and the key's next item opens a new group.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public void GroupByUntil_DurationFiresDuringPush_NoDeadlockNoLoss(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var time = new FakeTimeProvider();
        var source = new Subject<int>(); // never disposed on failure: completing a deadlocked operator would hang the test
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expiring = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var log = new ConcurrentQueue<string>();
        var subscription = source
            .GroupByUntil(
                static _ => 0,
                group => group.Debounce(TimeSpan.FromMinutes(1), time).Do(_ => expiring.TrySetResult()).AsUnitObservable())
            .Subscribe(group =>
            {
                log.Enqueue("open");
                group.Subscribe(
                    item =>
                    {
                        if (item == 2)
                        {
                            parked.TrySetResult();
                            release.Wait(cancellationToken);
                        }

                        log.Enqueue($"{item}");
                    },
                    _ => log.Enqueue("closed"));
            });
        source.OnNext(1);

        try
        {
            var pusher = Dedicated(() => source.OnNext(2), cancellationToken);
            Interleaving.Wait(parked.Task);
            var timer = Dedicated(() => time.Advance(TimeSpan.FromMinutes(1)), cancellationToken);
            Interleaving.Wait(expiring.Task); // the timer thread holds Debounce's lock and is about to close the group
            release.Set();
            Interleaving.Wait(pusher);
            Interleaving.Wait(timer);
        }
        finally
        {
            release.Set();
        }

        source.OnNext(3);
        log.ShouldBe(["open", "1", "2", "closed", "open", "3"]);
        subscription.Dispose();
        source.Dispose();
    }
}
