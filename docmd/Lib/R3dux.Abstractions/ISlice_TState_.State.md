#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[ISlice&lt;TState&gt;](ISlice_TState_.md 'R3dux.ISlice<TState>')

## ISlice<TState>.State Property

Gets an observable sequence that produces the state of this slice.

```csharp
R3.Observable<TState> State { get; }
```

#### Property Value
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TState](ISlice_TState_.md#R3dux.ISlice_TState_.TState 'R3dux.ISlice<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
The observable sequence of the state.