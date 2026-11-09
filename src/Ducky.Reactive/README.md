# Ducky.Reactive

Ducky.Reactive writes [Ducky](https://www.nuget.org/packages/Ducky) effects as [R3](https://github.com/Cysharp/R3) pipelines. A `ReactiveEffect` receives the stream of actions and the stream of states, and returns a stream of actions to dispatch. Use it where time and streams are the point (debouncing, polling, per-key switching); a plain `Effect<T>` from Ducky is simpler for everything else.

## Install

```shell
dotnet add package Ducky.Reactive --prerelease
```

## Example

The state and actions of a search box:

```csharp
using Ducky;

public sealed record Search(string Term, IReadOnlyList<string> Results);

public sealed record SearchTermChanged(string Term);

public sealed record SearchResultsArrived(string Term, IReadOnlyList<string> Results);

public sealed class SearchSlice : Slice<Search>
{
    public SearchSlice()
    {
        On<SearchTermChanged>((state, action) => state with { Term = action.Term });
        On<SearchResultsArrived>((state, action) => action.Term == state.Term ? state with { Results = action.Results } : state);
    }

    protected override Search Initial => new("", []);
}

public interface ISearchApi
{
    Task<IReadOnlyList<string>> SearchAsync(string term, CancellationToken cancellationToken);
}
```

The effect searches after a 300 ms pause in typing, and a newer term cancels the request in flight:

```csharp
using Ducky;
using Ducky.Reactive;
using R3;

public sealed class SearchEffect(ISearchApi api, TimeProvider time) : ReactiveEffect
{
    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<SearchTermChanged>()
            .Debounce(TimeSpan.FromMilliseconds(300), time) // a 300 ms pause in typing
            .SelectAwait(
                async (changed, token) => (object)new SearchResultsArrived(changed.Term, await api.SearchAsync(changed.Term, token)),
                AwaitOperation.Switch); // a newer term cancels the request in flight
}
```

The generated `AddDuckyGenerated_{Assembly}()` registers every reactive effect of the assembly; `AddReactiveEffect<T>()` registers one by hand:

```csharp
builder.Services.AddDucky(ducky => ducky
    .AddSlice<SearchSlice>()
    .AddReactiveEffect<SearchEffect>()); // created from the store's scope, disposed with the store
```

Inject `TimeProvider` for every time operator, so tests can drive the clock.

## Learn more

- Recipes (debounced search, polling, per-key switching): https://phmatray.github.io/Ducky/docs/guides/reactive/recipes.html
- Documentation: https://phmatray.github.io/Ducky/
- Changelog: https://github.com/phmatray/Ducky/blob/v2/CHANGELOG.md
- Issues and source: https://github.com/phmatray/Ducky

Ducky.Reactive is licensed under the Apache License 2.0.
