using R3;

namespace Ducky.Docs.Tests.Reactive;

#region reactive-group-by-until
// R3 ships no GroupBy or GroupByUntil. This operator closes a key's group, and releases it, when its duration observable
// emits or ends (completes or fails, as Rx.NET's GroupByUntil does); the key's next item opens a new group. The source's completion completes every open group. The lock guards the
// dictionary only: nothing is pushed while it is held, because a duration such as Debounce emits on a timer thread under
// its own lock, which a push into the group also takes. A duration that emits while an item is pushed into its group
// closes the group once that push ends, so no item is lost to a closed group. A durationSelector that throws fails the
// group it was opening and releases it, and the error goes on to the subscriber (OnErrorResume).
public static class GroupByUntilOperator
{
    public static Observable<Observable<T>> GroupByUntil<T, TKey>(this Observable<T> source, Func<T, TKey> keySelector, Func<Observable<T>, Observable<Unit>> durationSelector)
        where TKey : notnull =>
        Observable.Create<Observable<T>>(observer =>
        {
            var gate = new object();
            var groups = new Dictionary<TKey, Group<T>>();

            // Under the lock: removes the key only while it still maps to this group, so the group closes exactly once.
            bool Release(TKey key, Group<T> group) => groups.TryGetValue(key, out var current) && current == group && groups.Remove(key);

            Group<T>[] ReleaseAll()
            {
                lock (gate)
                {
                    Group<T>[] open = [.. groups.Values];
                    groups.Clear();
                    return open;
                }
            }

            var subscription = source.Subscribe(item =>
            {
                var key = keySelector(item);
                Group<T>? opened = null;
                Group<T> group;
                lock (gate)
                {
                    if (!groups.TryGetValue(key, out group!))
                    {
                        opened = group = new Group<T>();
                        groups.Add(key, group);
                    }

                    group.Pushing++;
                }

                if (opened is not null)
                {
                    void Expire()
                    {
                        lock (gate)
                        {
                            if (opened.Pushing > 0)
                            {
                                opened.ExpireRequested = true;
                                return;
                            }

                            if (!Release(key, opened))
                            {
                                return; // already closed: Take(1) completes right after it emits
                            }
                        }

                        opened.Items.OnCompleted();
                    }

                    try
                    {
                        observer.OnNext(opened.Items);
                        // Subscribed before the first item, which a Debounce duration must see. A duration that ends
                        // without emitting expires the group too, or the key would keep a dead group forever.
                        opened.Duration.Disposable = durationSelector(opened.Items).Take(1).Subscribe(_ => Expire(), static _ => { }, _ => Expire());
                    }
                    catch (Exception exception)
                    {
                        // A throwing durationSelector (or group subscriber) must not leave a group that never closes.
                        lock (gate)
                        {
                            _ = Release(key, opened);
                        }

                        opened.Items.OnCompleted(Result.Failure(exception));
                        throw;
                    }
                }

                group.Items.OnNext(item);
                lock (gate)
                {
                    group.Pushing--;
                    if (group.Pushing > 0 || !group.ExpireRequested || !Release(key, group))
                    {
                        return;
                    }
                }

                group.Items.OnCompleted();
            }, observer.OnErrorResume, result =>
            {
                foreach (var group in ReleaseAll())
                {
                    group.Duration.Dispose();
                    group.Items.OnCompleted(result);
                }

                observer.OnCompleted(result);
            });
            return Disposable.Create(() =>
            {
                subscription.Dispose();
                foreach (var group in ReleaseAll())
                {
                    group.Duration.Dispose();
                }
            });
        });

    private sealed class Group<T>
    {
        public Subject<T> Items { get; } = new();

        public SingleAssignmentDisposable Duration { get; } = new();

        public int Pushing { get; set; } // pushes in progress, guarded by the lock

        public bool ExpireRequested { get; set; } // the duration emitted during a push, guarded by the lock
    }
}
#endregion
