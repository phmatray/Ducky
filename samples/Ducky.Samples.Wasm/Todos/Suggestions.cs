using System.Collections.Immutable;
using System.Net.Http.Json;

namespace Ducky.Samples.Wasm.Todos;

public sealed record Suggestions(bool Loading, string? Error);

[DuckyAction]
[ActionType("suggestions/load")]
public sealed record LoadSuggestions;

public sealed record SuggestionsLoading;

public sealed record SuggestionsLoaded(ImmutableArray<string> Titles);

public sealed record SuggestionsFailed(string Error);

public sealed class SuggestionsSlice : Slice<Suggestions>
{
    public SuggestionsSlice()
    {
        On<SuggestionsLoading>(_ => new(Loading: true, Error: null));
        On<SuggestionsLoaded>(_ => new(Loading: false, Error: null));
        On<SuggestionsFailed>((_, action) => new(Loading: false, action.Error));
    }

    protected override Suggestions Initial => new(Loading: false, Error: null);
}

// The I/O leaves the reducers: the effect fetches wwwroot/sample-data/suggestions.json through the source-generated
// JSON context and dispatches what it got. Exhaust: clicks while a load runs are ignored.
public sealed class LoadSuggestionsEffect(HttpClient http) : Effect<LoadSuggestions>
{
    public override Concurrency Policy => Concurrency.Exhaust;

    public override Task Handle(LoadSuggestions action, EffectContext context, CancellationToken cancellationToken) =>
        context.Run(
            started: new SuggestionsLoading(),
            work: async token => await http.GetFromJsonAsync("sample-data/suggestions.json", SampleJson.Default.ImmutableArrayString, token).ConfigureAwait(false),
            succeeded: titles => new SuggestionsLoaded(titles),
            failed: exception => new SuggestionsFailed(exception.Message),
            cancellationToken);
}
