#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')

## R3dux.Abstractions Assembly
### Namespaces

<a name='R3dux'></a>

## R3dux Namespace

| Classes | |
| :--- | :--- |
| [R3duxException](R3duxException.md 'R3dux.R3duxException') | Exception type for the R3dux library. |

| Interfaces | |
| :--- | :--- |
| [IAction](IAction.md 'R3dux.IAction') | Defines a contract for an action. |
| [IDispatcher](IDispatcher.md 'R3dux.IDispatcher') | Defines the contract for a dispatcher that can dispatch actions and provide an observable stream of dispatched actions. |
| [IEffect](IEffect.md 'R3dux.IEffect') | Represents an effect that handles a stream of actions and interacts with the store's state. |
| [IKeyedAction](IKeyedAction.md 'R3dux.IKeyedAction') | Defines a contract for an action with a `type` key. |
| [IRootState](IRootState.md 'R3dux.IRootState') | Represents the root state of the application, managing slice states. |
| [IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer') | Provides methods for serializing and deserializing [IRootState](IRootState.md 'R3dux.IRootState') instances. |
| [ISlice](ISlice.md 'R3dux.ISlice') | Represents a state slice with basic state management capabilities. |
| [ISlice&lt;TState&gt;](ISlice_TState_.md 'R3dux.ISlice<TState>') | Represents a strongly-typed state slice with state management and reducers. |
| [IState](IState.md 'R3dux.IState') | Represents a state object. |
| [IStore](IStore.md 'R3dux.IStore') | Represents a store that manages application state and handles actions. |
