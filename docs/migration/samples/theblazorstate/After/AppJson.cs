using System.Text.Json.Serialization;

namespace TheBlazorState.After;

// UseJson(AppJson.Default): persistence and the prerender handoff serialize the slice states through it.
[JsonSerializable(typeof(DashboardState))]
[JsonSerializable(typeof(AppState))]
public sealed partial class AppJson : JsonSerializerContext;
