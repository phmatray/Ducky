using System.Text.Json.Serialization;

namespace TimeWarpSample.After;

// Ducky 2.0 serializes DevTools payloads through this context (DUCKY006 lists the types it lacks).
[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(IncrementCount))]
[JsonSerializable(typeof(LoadCount))]
[JsonSerializable(typeof(CountLoaded))]
[JsonSerializable(typeof(CountLoadFailed))]
public sealed partial class AppJson : JsonSerializerContext;
