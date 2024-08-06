#### [R3dux.Selectors](R3dux.Selectors.md 'R3dux.Selectors')
### [R3dux](R3dux.Selectors.md#R3dux 'R3dux').[MemoizedSelector](MemoizedSelector.md 'R3dux.MemoizedSelector')

## MemoizedSelector.Create<TState,TResult>(Func<TState,TResult>, Func<TState,object>[]) Method

Creates a memoized selector that caches the results of the selector function based on the provided dependencies.

```csharp
public static System.Func<TState,TResult> Create<TState,TResult>(System.Func<TState,TResult> selector, params System.Func<TState,object>[] dependencies)
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TState'></a>

`TState`

The type of the state.

<a name='R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TResult'></a>

`TResult`

The type of the result.
#### Parameters

<a name='R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).selector'></a>

`selector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TResult 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[]).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The selector function to be memoized.

<a name='R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).dependencies'></a>

`dependencies` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The functions representing dependencies that the selector relies on.

#### Returns
[System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TState 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[]).TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](MemoizedSelector.Create_TState,TResult_(Func_TState,TResult_,Func_TState,object_[]).md#R3dux.MemoizedSelector.Create_TState,TResult_(System.Func_TState,TResult_,System.Func_TState,object_[]).TResult 'R3dux.MemoizedSelector.Create<TState,TResult>(System.Func<TState,TResult>, System.Func<TState,object>[]).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')  
A memoized selector function.