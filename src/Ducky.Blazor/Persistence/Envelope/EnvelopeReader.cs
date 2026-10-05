using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

// The one envelope reader, shared by hydration and cross-tab receipt (SPEC §11.5, §11.7). It never throws: every value
// that yields no state reads as not found, and the caller keeps the slice's current state.
internal sealed class EnvelopeReader(DuckyJson json, ILogger logger)
{
    private readonly SafeLogger _logger = new(logger);

    /// <summary>Reads <paramref name="text"/> as an envelope of <paramref name="stateType"/> at <paramref name="version"/>.</summary>
    /// <param name="text">The stored value.</param>
    /// <param name="key">The storage key, named in the logs.</param>
    /// <param name="stateType">The slice's declared state type (§10).</param>
    /// <param name="version">The slice's current version.</param>
    /// <param name="migrations">The steps by their from-version, each one version forward.</param>
    /// <param name="maxAge">Discards an envelope written longer ago than this; null keeps every envelope.</param>
    /// <param name="now">The current time.</param>
    /// <param name="state">The state, or null when not found.</param>
    /// <returns>True when <paramref name="text"/> yielded a state.</returns>
    internal bool TryRead(
        string text, string key, Type stateType, int version, IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> migrations,
        TimeSpan? maxAge, DateTimeOffset now, [NotNullWhen(true)] out object? state)
    {
        state = null;

        // The options' MaxDepth bounds s; the envelope adds one level above it.
        var options = new JsonDocumentOptions { MaxDepth = (json.Options.MaxDepth == 0 ? 64 : json.Options.MaxDepth) + 1 };
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, options);
        }

        // ArgumentException: text that is not valid UTF-16 (a lone surrogate) cannot be transcoded to JSON.
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            Log.UnreadableEnvelope(_logger, exception, key, stateType);
            return false;
        }

        using (document)
        {
            var root = document.RootElement;

            // 1.x data (an AssemblyQualifiedName and no version) and any other foreign value fail here (ADR-0033).
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var found)
                || !root.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String || !at.TryGetDateTimeOffset(out var writtenAt)
                || !root.TryGetProperty("s", out var payload))
            {
                Log.NotAnEnvelope(_logger, key);
                return false;
            }

            // Migrations only run forward: a newer envelope (a rollback, a stale tab) is not found (D18, ADR-0047).
            if (found > version)
            {
                Log.NewerEnvelope(_logger, key, found, version);
                return false;
            }

            if (now - writtenAt > maxAge)
            {
                Log.ExpiredEnvelope(_logger, key);
                return false;
            }

            if (found < version)
            {
                using var migrated = Migrate(payload, found, version, migrations, options, key, stateType);
                return migrated is not null && TryDeserialize(migrated.RootElement, key, stateType, out state);
            }

            return TryDeserialize(payload, key, stateType, out state);
        }
    }

    // Runs the steps from..version-1 in order; a missing step (KeyNotFoundException) fails like a throwing one.
    private JsonDocument? Migrate(
        JsonElement payload, int from, int version, IReadOnlyDictionary<int, Func<JsonNode, JsonNode>> migrations, JsonDocumentOptions options,
        string key, Type stateType)
    {
        try
        {
            var node = JsonNode.Parse(payload.GetRawText(), documentOptions: options);
            for (var step = from; step < version; step++)
            {
                node = migrations[step](node!);
            }

            return JsonDocument.Parse(node?.ToJsonString() ?? "null", options);
        }
#pragma warning disable CA1031 // justification: a user migration step never escapes the reader (§11.5); the Warning carries it
        catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
        {
            Log.UnreadableEnvelope(_logger, exception, key, stateType);
            return null;
        }
    }

    // Through the store's gateway with the declared type (§10), which logs its own Warning on failure; JSON null is no state.
    private bool TryDeserialize(JsonElement payload, string key, Type stateType, [NotNullWhen(true)] out object? state)
    {
        if (!json.TryDeserialize(payload, stateType, out state, key))
        {
            return false;
        }

        if (state is null)
        {
            Log.UnreadableEnvelope(_logger, null, key, stateType);
            return false;
        }

        return true;
    }
}
