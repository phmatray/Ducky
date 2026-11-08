using System.Text.Json.Serialization;

namespace FluxorSample.After;

#region after-json
// Ducky 2.0 never serializes by reflection: the prerender seed, persistence and DevTools go through this context.
[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(IncrementCounterAction))]
[JsonSerializable(typeof(LoadCounterAction))]
[JsonSerializable(typeof(LoadCounterResultAction))]
[JsonSerializable(typeof(LoadCounterFailedAction))]
public sealed partial class AppJson : JsonSerializerContext;
#endregion
