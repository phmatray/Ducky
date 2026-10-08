using System.Buffers;
using System.Text.Json;

namespace Ducky.Blazor;

// SPEC §11.4 OnPersisting, once the store is idle: the seed {"v":1,"src":…,"dirty":[…]?,"ver":{…}?,"slices":{key: state}}
// of the Prerender<T> slices, everything from one snapshot, in slice registration order, within PrerenderSeedMaxWireBytes
// by the wire estimate.
internal static class SeedWriter
{
    // The page's state dictionary entry plus the data-protection header, IV, padding and MAC (§11.4).
    private const int Overhead = 256;

    /// <summary>
    /// The seed of <paramref name="registration"/>'s Prerender&lt;T&gt; slices of <paramref name="store"/>, every value read
    /// from one snapshot. A slice whose include predicate is false is left out (Debug 2011); one whose predicate throws,
    /// whose state can't be serialized, or whose inclusion would push the estimate past the budget, with Warning 2021.
    /// Each persisted key carried records its version; a pause seed lists the carried browser-storage keys that are dirty against
    /// <paramref name="persistence"/>'s baselines.
    /// </summary>
    public static byte[] Write(IStore store, BlazorRegistration registration, string src, PersistenceMiddleware persistence, SafeLogger logger)
    {
        var snap = store.State;
        List<Carried> slices = [];
        var seed = Envelope(src, slices);
        foreach (var slice in store.Slices)
        {
            if (!registration.Prerender.TryGetValue(slice.GetType(), out var include))
            {
                continue;
            }

            var state = snap.Get(slice.Key);
            Exception? failure = null;
            try
            {
                if (include?.Invoke(state) == false)
                {
                    Log.SeedSliceExcluded(logger, slice.Key);
                    continue;
                }
            }
#pragma warning disable CA1031 // justification: a throwing predicate only leaves its slice out, as a throwing getter does (§10)
            catch (Exception exception) when (exception is not OutOfMemoryException)
#pragma warning restore CA1031
            {
                failure = exception;
            }

            if (failure is null && store.Json.TrySerialize(state, slice.StateType, out var json))
            {
                // Only a browser-storage key can be dirty: the pause override never applies to Server storage (§11.4).
                var options = registration.Persist.GetValueOrDefault(slice.GetType())?.Options;
                slices.Add(new(slice.Key, json, options?.Version, options is { Storage: not PersistStorage.Server } && src == "pause" && IsDirty(store, persistence, snap, slice, json)));
                var candidate = Envelope(src, slices);
                if (WireEstimate(candidate.Length) <= registration.Options.PrerenderSeedMaxWireBytes)
                {
                    seed = candidate;
                    continue;
                }

                slices.RemoveAt(slices.Count - 1);
            }

            Log.SeedSliceOmitted(logger, slice.Key, failure);
        }

        return seed;
    }

    /// <summary>
    /// What <paramref name="n"/> envelope bytes add to the Interactive Server start-circuit message: the base64 JSON string
    /// of <c>PersistAsJson&lt;byte[]&gt;</c>, base64 again inside the state dictionary, data-protected and base64 again.
    /// </summary>
    public static long WireEstimate(long n)
    {
        var e = (4 * Thirds(n)) + 2;
        return 4 * Thirds((4 * Thirds(e)) + Overhead);
    }

    private static long Thirds(long bytes) => (bytes + 2) / 3;

    // §11.4 pause: dirty iff the local serialization in snap differs from LastKnownPayload (the last successful write, read
    // or receipt), an absent baseline comparing as the initial state's serialization (§11.7): the circuit holds a change
    // storage may not have. The key is named for snap's epoch; a scope not known for it names no stored key, so the value
    // compares with the initial state (M12-07 leaves such keys out of the seed).
    private static bool IsDirty(IStore store, PersistenceMiddleware persistence, StateSnapshot snap, Slice slice, string json)
    {
        var key = $"{persistence.WritePrefix(snap.Get<PersistenceState>().ScopeEpoch)}:{slice.Key}";
        _ = store.Json.TrySerialize(store.InitialState.Get(slice.Key), slice.StateType, out var initial);
        return json != persistence.LastKnownPayload.GetValueOrDefault(key, initial!);
    }

    // ponytail: rewritten whole for each candidate slice, O(slices²) bytes, bounded by the budget (20 KB by default).
    private static byte[] Envelope(string src, List<Carried> slices)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteString("src", src);
            if (slices.Any(static slice => slice.Dirty))
            {
                writer.WriteStartArray("dirty");
                foreach (var slice in slices.Where(static slice => slice.Dirty))
                {
                    writer.WriteStringValue(slice.Key);
                }

                writer.WriteEndArray();
            }

            if (slices.Any(static slice => slice.Version is not null))
            {
                writer.WriteStartObject("ver");
                foreach (var slice in slices.Where(static slice => slice.Version is not null))
                {
                    writer.WriteNumber(slice.Key, slice.Version!.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteStartObject("slices");
            foreach (var slice in slices)
            {
                writer.WritePropertyName(slice.Key);

                // The serializer's own output, valid at the options' MaxDepth (as EnvelopeWriter writes it).
                writer.WriteRawValue(slice.Json, skipInputValidation: true);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    // One slice in the seed: its state, its PersistOptions.Version when persisted, and whether a pause found it dirty.
    private sealed record Carried(string Key, string Json, int? Version, bool Dirty);
}
