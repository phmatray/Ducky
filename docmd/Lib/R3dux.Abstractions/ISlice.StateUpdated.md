#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[ISlice](ISlice.md 'R3dux.ISlice')

## ISlice.StateUpdated Property

Gets an observable sequence that produces a notification when the state is updated.

```csharp
R3.Observable<R3.Unit> StateUpdated { get; }
```

#### Property Value
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3.Unit](https://docs.microsoft.com/en-us/dotnet/api/R3.Unit 'R3.Unit')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
The observable sequence of state updates.