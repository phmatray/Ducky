#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## ISlice<TState> Interface

Represents a strongly-typed state slice with state management and reducers.

```csharp
public interface ISlice<TState> :
R3dux.ISlice
```
#### Type parameters

<a name='R3dux.ISlice_TState_.TState'></a>

`TState`

The type of the state managed by this slice.

Implements [ISlice](ISlice.md 'R3dux.ISlice')

| Properties | |
| :--- | :--- |
| [Reducers](ISlice_TState_.Reducers.md 'R3dux.ISlice<TState>.Reducers') | Gets the collection of reducers for this state slice. |
| [State](ISlice_TState_.State.md 'R3dux.ISlice<TState>.State') | Gets an observable sequence that produces the state of this slice. |
