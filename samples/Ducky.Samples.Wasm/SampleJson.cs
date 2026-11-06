using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Ducky.Samples.Wasm.Todos;

namespace Ducky.Samples.Wasm;

// The only JSON path of the app (no reflection, trim-safe): persistence and DevTools serialize through it (UseJson), and
// the suggestions effect reads its file through it. DUCKY006 lists any [DuckyAction] or state type left out; the
// effect's lifecycle actions (SuggestionsLoading/Loaded/Failed) are not [DuckyAction] types, so they are listed by hand.
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(TodoList))]
[JsonSerializable(typeof(TodoFilter))]
[JsonSerializable(typeof(Suggestions))]
[JsonSerializable(typeof(TodoAdded))]
[JsonSerializable(typeof(TodoToggled))]
[JsonSerializable(typeof(TodoRemoved))]
[JsonSerializable(typeof(LoadSuggestions))]
[JsonSerializable(typeof(SuggestionsLoading))]
[JsonSerializable(typeof(SuggestionsLoaded))]
[JsonSerializable(typeof(SuggestionsFailed))]
[JsonSerializable(typeof(ImmutableArray<string>))]
internal sealed partial class SampleJson : JsonSerializerContext;
