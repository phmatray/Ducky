using System.Buffers;
using System.Text.Json;

namespace Ducky.Blazor;

// SPEC §11.4 OnPersisting, once the store is idle: the seed {"v":1,"src":…,"slices":{key: state}} of the Prerender<T>
// slices, from one snapshot, in slice registration order, within PrerenderSeedMaxWireBytes by the wire estimate.
internal static class SeedWriter
{
    // The page's state dictionary entry plus the data-protection header, IV, padding and MAC (§11.4).
    private const int Overhead = 256;

    /// <summary>
    /// The seed of <paramref name="registration"/>'s Prerender&lt;T&gt; slices of <paramref name="store"/>, every value read
    /// from one snapshot. A slice whose include predicate is false is left out (Debug 2011); one whose predicate throws,
    /// whose state can't be serialized, or whose inclusion would push the estimate past the budget, with Warning 2021.
    /// </summary>
    public static byte[] Write(IStore store, BlazorRegistration registration, string src, SafeLogger logger)
    {
        var snap = store.State;
        List<KeyValuePair<string, string>> slices = [];
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
                slices.Add(new(slice.Key, json));
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

    // ponytail: rewritten whole for each candidate slice, O(slices²) bytes, bounded by the budget (20 KB by default).
    private static byte[] Envelope(string src, List<KeyValuePair<string, string>> slices)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteString("src", src);
            writer.WriteStartObject("slices");
            foreach (var (key, json) in slices)
            {
                writer.WritePropertyName(key);

                // The serializer's own output, valid at the options' MaxDepth (as EnvelopeWriter writes it).
                writer.WriteRawValue(json, skipInputValidation: true);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
