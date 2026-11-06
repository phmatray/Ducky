using System.Collections.Concurrent;

namespace TheBlazorState.Storage;

/// <summary>
/// Singleton service for server-side cross-circuit notifications.
/// When one Blazor Server circuit changes a [Persist]+LocalStorage value,
/// it publishes to the hub. All other circuits subscribed to that key
/// receive the update and can re-render.
///
/// This eliminates the need for client-side mechanisms (BroadcastChannel,
/// storage events) which are unreliable with Blazor Server's JSInterop.
///
/// Callbacks are dispatched asynchronously via <see cref="Task.Run"/> so
/// each circuit processes notifications on its own thread, not on the
/// publisher's thread. This prevents cross-circuit thread contamination
/// and allows <c>InvokeAsync(StateHasChanged)</c> to dispatch correctly
/// to each circuit's synchronization context.
/// </summary>
public sealed class CrossTabHub : IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Subscription>> _subscriptions = new();
    private int _nextId;

    /// <summary>
    /// Subscribe to changes for a specific key. Returns a disposable
    /// that unsubscribes when disposed.
    /// </summary>
    /// <param name="key">The storage key to subscribe to.</param>
    /// <param name="callback">Called with (key, rawJson) when another circuit changes the value.</param>
    /// <param name="subscriberId">Unique ID for this subscriber (circuit). Used to prevent self-notification.</param>
    public IDisposable Subscribe(string key, Action<string, string> callback, string? subscriberId = null)
    {
        var id = subscriberId ?? $"auto-{Interlocked.Increment(ref _nextId)}";
        var sub = new Subscription(id, callback);

        var keySubscriptions = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<string, Subscription>());
        keySubscriptions[sub.Id] = sub;

        return new Unsubscriber(keySubscriptions, sub.Id);
    }

    /// <summary>
    /// Publish a value change to all subscribers of the given key,
    /// except the publisher itself. Callbacks are dispatched asynchronously
    /// so each circuit processes the notification on its own thread.
    /// </summary>
    /// <param name="key">The storage key that changed.</param>
    /// <param name="rawJson">The serialized envelope JSON.</param>
    /// <param name="publisherId">The circuit ID of the publisher (will be excluded).</param>
    public void Publish(string key, string rawJson, string publisherId)
    {
        if (!_subscriptions.TryGetValue(key, out var keySubscriptions))
            return;

        foreach (var kvp in keySubscriptions)
        {
            if (kvp.Key != publisherId)
            {
                var callback = kvp.Value.Callback;
                _ = Task.Run(() =>
                {
                    try
                    {
                        callback(key, rawJson);
                    }
                    catch
                    {
                        // Subscriber's circuit may be disposed; ignore
                    }
                });
            }
        }
    }

    public void Dispose()
    {
        _subscriptions.Clear();
    }

    private sealed record Subscription(string Id, Action<string, string> Callback);

    private sealed class Unsubscriber(ConcurrentDictionary<string, Subscription> subscriptions, string id) : IDisposable
    {
        public void Dispose() => subscriptions.TryRemove(id, out _);
    }
}
