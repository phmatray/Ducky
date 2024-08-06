#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[ISlice&lt;TState&gt;](ISlice_TState_.md 'R3dux.ISlice<TState>')

## ISlice<TState>.Reducers Property

Gets the collection of reducers for this state slice.

```csharp
System.Collections.Generic.Dictionary<System.Type,System.Func<TState,R3dux.IAction,TState>> Reducers { get; }
```

#### Property Value
[System.Collections.Generic.Dictionary&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.Dictionary-2 'System.Collections.Generic.Dictionary`2')[System.Type](https://docs.microsoft.com/en-us/dotnet/api/System.Type 'System.Type')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.Dictionary-2 'System.Collections.Generic.Dictionary`2')[System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[TState](ISlice_TState_.md#R3dux.ISlice_TState_.TState 'R3dux.ISlice<TState>.TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[IAction](IAction.md 'R3dux.IAction')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[TState](ISlice_TState_.md#R3dux.ISlice_TState_.TState 'R3dux.ISlice<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.Dictionary-2 'System.Collections.Generic.Dictionary`2')  
The collection of reducers.