#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.State Property

Gets an observable sequence that produces the state of this slice.

```csharp
public virtual R3.Observable<TState> State { get; }
```

Implements [State](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice-1.State 'R3dux.ISlice`1.State')

#### Property Value
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
The observable sequence of the state.