#### [R3dux](R3dux.md 'R3dux')

## R3dux Assembly
### Namespaces

<a name='R3dux'></a>

## R3dux Namespace

| Classes | |
| :--- | :--- |
| [DependencyInjections](DependencyInjections.md 'R3dux.DependencyInjections') | Extension methods for adding R3dux services to the dependency injection container. |
| [Dispatcher](Dispatcher.md 'R3dux.Dispatcher') | A dispatcher that queues and dispatches actions, providing an observable stream of dispatched actions. |
| [Effect](Effect.md 'R3dux.Effect') | Represents an effect that handles a stream of actions and interacts with the store's state. |
| [LoggerProvider](LoggerProvider.md 'R3dux.LoggerProvider') | Provides a centralized mechanism to configure and obtain logger instances. |
| [ObservableSlices](ObservableSlices.md 'R3dux.ObservableSlices') | Manages a collection of observable slices and provides an observable root state. |
| [R3duxLogMessages](R3duxLogMessages.md 'R3dux.R3duxLogMessages') | |
| [R3duxOptions](R3duxOptions.md 'R3dux.R3duxOptions') | Options for configuring R3dux services. |
| [R3duxStore](R3duxStore.md 'R3dux.R3duxStore') | Represents a store that manages application state and handles actions. |
| [RootState](RootState.md 'R3dux.RootState') | Represents the root state of the application, managing slice states. |
| [RootStateSerializer](RootStateSerializer.md 'R3dux.RootStateSerializer') | Provides methods for serializing and deserializing [RootState](RootState.md 'R3dux.RootState') instances. |
| [SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>') | Represents a strongly-typed state slice with state management and reducers. |
| [StateChange&lt;TState&gt;](StateChange_TState_.md 'R3dux.StateChange<TState>') | Represents a state change notification. |
| [StateLoggerObserver&lt;TState&gt;](StateLoggerObserver_TState_.md 'R3dux.StateLoggerObserver<TState>') | Provides logging functionalities for state changes. |
| [StoreFactory](StoreFactory.md 'R3dux.StoreFactory') | Factory for creating instances of [R3duxStore](R3duxStore.md 'R3dux.R3duxStore'). |
| [StoreInitialized](StoreInitialized.md 'R3dux.StoreInitialized') | Represents an action that is dispatched when the store is initialized. |

| Structs | |
| :--- | :--- |
| [R3duxLogMessages.__LogStateChangeStruct](R3duxLogMessages.__LogStateChangeStruct.md 'R3dux.R3duxLogMessages.__LogStateChangeStruct') | This API supports the logging infrastructure and is not intended to be used directly from your code. It is subject to change in the future. |
