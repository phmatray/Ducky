using System.Text.Json.Serialization;

namespace Ducky1x.After;

#region after-json
// Checklist step 3: a JsonSerializerContext passed with UseJson(AppJson.Default). It may start empty: DUCKY006 then lists
// each [DuckyAction] and slice state type it lacks.
[JsonSerializable(typeof(CounterState))]
[JsonSerializable(typeof(Increment))]
[JsonSerializable(typeof(Reset))]
public sealed partial class AppJson : JsonSerializerContext;
#endregion
