using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Web;

/// <summary>A versioned snapshot.</summary>
public sealed record Snapshot(string Key, int Version);

/// <summary>Keeps the newest snapshot per key; older versions are rejected and logged.</summary>
public sealed class SnapshotStore(ILogger<SnapshotStore> logger)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Snapshot> _items = [];

    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _items.Count;
            }
        }
    }

    /// <summary>Accepts the JSON snapshot unless a newer or equal version is already stored.</summary>
    public bool Accept(string json)
    {
        var snapshot = JsonSerializer.Deserialize(json, WebJsonContext.Default.Snapshot)
            ?? throw new ArgumentException("null snapshot", nameof(json));
        lock (_gate)
        {
            if (_items.TryGetValue(snapshot.Key, out var current) && current.Version >= snapshot.Version)
            {
                Log.Rejected(logger, snapshot.Key, snapshot.Version, current.Version);
                return false;
            }

            _items[snapshot.Key] = snapshot;
            return true;
        }
    }

    /// <summary>Accepts the snapshot a loader produces.</summary>
    public async Task<bool> AcceptAsync(Func<Task<string>> load)
    {
        ArgumentNullException.ThrowIfNull(load);
        return Accept(await load().ConfigureAwait(ConfigureAwaitOptions.None));
    }

    /// <summary>The stored snapshot as JSON.</summary>
    public string Export(string key)
    {
        lock (_gate)
        {
            return JsonSerializer.Serialize(_items[key], WebJsonContext.Default.Snapshot);
        }
    }
}

[JsonSerializable(typeof(Snapshot))]
internal sealed partial class WebJsonContext : JsonSerializerContext;

internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Snapshot {Key} v{Version} rejected: v{Current} is stored")]
    public static partial void Rejected(ILogger logger, string key, int version, int current);
}
