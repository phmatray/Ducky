using System.Text.Json.Serialization;

namespace Ducky.Blazor;

// SPEC §10: Ducky.Blazor's one wire JSON context, for fixed wire types only (the persistence and prerender envelopes,
// the DevTools messages and the projections of library actions, added with their features). It holds no user or
// circuit data, which makes it the one accepted static cache (§17.5).
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SeedEnvelope))]
internal sealed partial class BlazorWireContext : JsonSerializerContext;
