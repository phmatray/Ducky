#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor](R3dux.Blazor.md#R3dux.Blazor 'R3dux.Blazor')

## R3duxComponent<TState> Class

A base component class for R3dux components that manages state and dispatches actions.

```csharp
public abstract class R3duxComponent<TState> : Microsoft.AspNetCore.Components.ComponentBase,
System.IDisposable
    where TState : notnull
```
#### Type parameters

<a name='R3dux.Blazor.R3duxComponent_TState_.TState'></a>

`TState`

The type of the state managed by this component.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Microsoft.AspNetCore.Components.ComponentBase](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ComponentBase 'Microsoft.AspNetCore.Components.ComponentBase') &#129106; R3duxComponent<TState>

Derived  
&#8627; [R3duxLayout&lt;TState&gt;](R3duxLayout_TState_.md 'R3dux.Blazor.R3duxLayout<TState>')

Implements [System.IDisposable](https://docs.microsoft.com/en-us/dotnet/api/System.IDisposable 'System.IDisposable')

| Properties | |
| :--- | :--- |
| [ComponentName](R3duxComponent_TState_.ComponentName.md 'R3dux.Blazor.R3duxComponent<TState>.ComponentName') | Gets the name of the component. |
| [Dispatcher](R3duxComponent_TState_.Dispatcher.md 'R3dux.Blazor.R3duxComponent<TState>.Dispatcher') | Gets or sets the dispatcher used to dispatch actions to the store. |
| [Logger](R3duxComponent_TState_.Logger.md 'R3dux.Blazor.R3duxComponent<TState>.Logger') | Gets or sets the logger used for logging information. |
| [State](R3duxComponent_TState_.State.md 'R3dux.Blazor.R3duxComponent<TState>.State') | Gets the current state of the component. |
| [StateObservable](R3duxComponent_TState_.StateObservable.md 'R3dux.Blazor.R3duxComponent<TState>.StateObservable') | Gets an observable stream of the state managed by this component. |
| [Store](R3duxComponent_TState_.Store.md 'R3dux.Blazor.R3duxComponent<TState>.Store') | Gets or sets the store that manages the application state. |

| Methods | |
| :--- | :--- |
| [Dispatch(IAction)](R3duxComponent_TState_.Dispatch(IAction).md 'R3dux.Blazor.R3duxComponent<TState>.Dispatch(R3dux.IAction)') | Dispatches an action to the store. |
| [Dispose()](R3duxComponent_TState_.Dispose().md 'R3dux.Blazor.R3duxComponent<TState>.Dispose()') | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. |
| [Dispose(bool)](R3duxComponent_TState_.Dispose(bool).md 'R3dux.Blazor.R3duxComponent<TState>.Dispose(bool)') | Releases the unmanaged resources used by the [R3duxComponent&lt;TState&gt;](R3duxComponent_TState_.md 'R3dux.Blazor.R3duxComponent<TState>') and optionally releases the managed resources. |
| [OnAfterSubscribed()](R3duxComponent_TState_.OnAfterSubscribed().md 'R3dux.Blazor.R3duxComponent<TState>.OnAfterSubscribed()') | Invoked after the state subscription has been established.<br/>This method is intended to be overridden by derived classes. |
