using System.Text.Json.Serialization;

namespace Ducky.Blazor;

// SPEC §10: Ducky.Blazor's one wire JSON context, for fixed wire types only (the persistence and prerender envelopes,
// the DevTools messages and the projections of library actions, added with their features). It holds no user or
// circuit data, which makes it the one accepted static cache (§17.5).
// PersistenceStatus by name, so DevTools shows "Hydrated", not 2 (§11.8).
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, Converters = [typeof(JsonStringEnumConverter<PersistenceStatus>)])]
[JsonSerializable(typeof(SeedEnvelope))]
[JsonSerializable(typeof(DevToolsConfig))]
[JsonSerializable(typeof(ReducerFailure))]
[JsonSerializable(typeof(EffectFailure))]
[JsonSerializable(typeof(StoreInitialized))]
[JsonSerializable(typeof(HydrationCompleted))]
[JsonSerializable(typeof(HydrationFailed))]
[JsonSerializable(typeof(PersistenceFailed))]
[JsonSerializable(typeof(ClearPersistedState))]
[JsonSerializable(typeof(PersistenceState))]
internal sealed partial class BlazorWireContext : JsonSerializerContext;
