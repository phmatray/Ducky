using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Ducky.Blazor;

// The persisted envelope {"v":2,"at":"2026-09-30T12:00:00Z","s":<state>} (SPEC §11.5): s is the payload the caller
// serialized with the slice's declared type (§10) and compares for dedupe; no type name is ever written (ADR-0033).
internal static class EnvelopeWriter
{
    internal static string Write(string payload, int version, DateTimeOffset at)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", version);
            writer.WriteString("at", at.UtcDateTime);
            writer.WritePropertyName("s");

            // The payload is the serializer's own output, valid at the options' MaxDepth; re-validating it would cap it at
            // the reader default of 64 levels and refuse a state the options allow (the reader allows MaxDepth + 1).
            writer.WriteRawValue(payload, skipInputValidation: true);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
