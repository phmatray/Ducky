using System.Buffers;
using System.Text.Json;

namespace Ducky.Blazor;

// The prerender seed {"v":1,"slices":{key: state}} (SPEC §11.4), persisted as the UTF-8 bytes of this envelope. Read
// through BlazorWireContext: each state stays a JsonElement, which the store's Restore deserializes with the slice's
// declared type (§10). The pause fields (src, scope, dirty, ver) join it with seed writing and pause seeds.
// Write is an interim stub until M6-05: idle wait (PrerenderIdleTimeout, Warning 2014), src, PrerenderSeedMaxWireBytes
// budget, Warning 2021 for an unserializable slice, Debug log for include=false.
internal sealed record SeedEnvelope(Dictionary<string, object>? Slices)
{
    /// <summary>
    /// Writes the seed of the <paramref name="prerender"/> slices of <paramref name="store"/> from one snapshot, in
    /// registration order. A slice whose include predicate is false, or whose state can't be serialized, is left out.
    /// </summary>
    public static byte[] Write(IStore store, IReadOnlyDictionary<Type, Func<object, bool>?> prerender)
    {
        var snap = store.State;
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteStartObject("slices");
            foreach (var slice in store.Slices)
            {
                if (!prerender.TryGetValue(slice.GetType(), out var include))
                {
                    continue;
                }

                var state = snap.Get(slice.Key);
                if ((include is null || include(state)) && store.Json.TrySerialize(state, slice.StateType, out var json))
                {
                    writer.WritePropertyName(slice.Key);

                    // The serializer's own output, valid at the options' MaxDepth (as EnvelopeWriter writes it).
                    writer.WriteRawValue(json, skipInputValidation: true);
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
