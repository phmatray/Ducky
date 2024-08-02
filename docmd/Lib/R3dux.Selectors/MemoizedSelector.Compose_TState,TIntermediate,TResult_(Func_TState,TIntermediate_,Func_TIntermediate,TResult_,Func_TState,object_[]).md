#### [R3dux.Selectors](R3dux.Selectors.md 'R3dux.Selectors')
### [R3dux](R3dux.Selectors.md#R3dux 'R3dux').[MemoizedSelector](MemoizedSelector.md 'R3dux.MemoizedSelector')

## MemoizedSelector.Compose<TState,TIntermediate,TResult>(Func<TState,TIntermediate>, Func<TIntermediate,TResult>, Func<TState,object>[]) Method

Composes two selector functions into one, allowing for efficient state transformations.

```csharp
public static System.Func<TState,TResult> Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate> intermediateSelector, System.Func<TIntermediate,TResult> finalSelector, params System.Func<TState,object>[] dependencies)
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TState'></a>

`TState`

The type of the state.

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TIntermediate'></a>

`TIntermediate`

The type of the intermediate result.

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TResult'></a>

`TResult`

The type of the final result.
#### Parameters

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).intermediateSelector'></a>

`intermediateSelector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TIntermediate](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TIntermediate 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TIntermediate')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The intermediate selector function.

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).finalSelector'></a>

`finalSelector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TIntermediate](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TIntermediate 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TIntermediate')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TResult 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The final selector function.

<a name='R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).dependencies'></a>

`dependencies` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The functions representing dependencies that the selectors rely on.

#### Returns
[System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](MemoizedSelector.Compose_TState,TIntermediate,TResult_(Func_TState,TIntermediate_,Func_TIntermediate,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Compose_TState,TIntermediate,TResult_(System.Func_TState,TIntermediate_,System.Func_TIntermediate,TResult_,System.Func_TState,object_[]).TResult 'R3dux.MemoizedSelector.Compose<TState,TIntermediate,TResult>(System.Func<TState,TIntermediate>, System.Func<TIntermediate,TResult>, System.Func<TState,object>[]).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')  
A memoized selector function that composes the intermediate and final selectors.