#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.StateUpdated Property

Gets an observable sequence that produces a notification when the state is updated.

```csharp
public virtual R3.Observable<R3.Unit> StateUpdated { get; }
```

Implements [StateUpdated](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice.StateUpdated 'R3dux.ISlice.StateUpdated')

#### Property Value
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3.Unit](https://docs.microsoft.com/en-us/dotnet/api/R3.Unit 'R3.Unit')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
The observable sequence of state updates.