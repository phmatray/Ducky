#### [R3dux.Selectors](R3dux.Selectors.md 'R3dux.Selectors')
### [R3dux](R3dux.Selectors.md#R3dux 'R3dux')

## MemoizedSelector Class

Provides memoization functionalities for selectors to minimize re-computations  
and allow for efficient state management.

```csharp
public static class MemoizedSelector
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; MemoizedSelector

| Methods | |
| :--- | :--- |
| [AreDependenciesEqual(object[], object[])](MemoizedSelector.AreDependenciesEqual(object[],object[]).md 'R3dux.MemoizedSelector.AreDependenciesEqual(object[], object[])') | Compares two sets of dependencies to determine if they are equal. |
| [Compose&lt;TState,TIntermediate,TResult&gt;(Func&lt;TState,TIntermediate&gt;, Func&lt;TIntermediate,TResult&gt;, Func&lt;TState,object&gt;[])](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[])') | Composes two selector functions into one, allowing for efficient state transformations. |
| [Create&lt;TState,TResult&gt;(Func&lt;TState,TResult&gt;, Func&lt;TState,object&gt;[])](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[])') | Creates a memoized selector that caches the results of the selector function based on the provided dependencies. |
