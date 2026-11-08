using System.Text.Json.Serialization;

namespace StateContainerSample.After;

// The prerender seed is serialized through this context, never by reflection (DUCKY306 names a missing state type).
[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(LoadCount))]
public sealed partial class AppJson : JsonSerializerContext;
