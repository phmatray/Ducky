#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.InvokeService<TAction,TResult>(this Observable<TAction>, Func<TAction,ValueTask<TResult>>, Func<TResult,IAction>, Func<Exception,IAction>) Method

Invokes a service call for each element in the observable sequence, returning a sequence of actions based on the result or error.

```csharp
public static R3.Observable<R3dux.IAction> InvokeService<TAction,TResult>(this R3.Observable<TAction> source, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>> serviceCall, System.Func<TResult,R3dux.IAction> successSelector, System.Func<System.Exception,R3dux.IAction> errorSelector);
```
#### Type parameters

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TAction'></a>

`TAction`

The type of the source elements, which are actions to be processed.

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TResult'></a>

`TResult`

The type of the result from the service call.
#### Parameters

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TAction](CustomOperators.InvokeService_TAction,TResult_(thisObservable_TAction_,Func_TAction,ValueTask_TResult__,Func_TResult,IAction_,Func_Exception,IAction_).md#R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TAction 'R3dux.CustomOperators.InvokeService<TAction,TResult>(this R3.Observable<TAction>, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>>, System.Func<TResult,R3dux.IAction>, System.Func<System.Exception,R3dux.IAction>).TAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).serviceCall'></a>

`serviceCall` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TAction](CustomOperators.InvokeService_TAction,TResult_(thisObservable_TAction_,Func_TAction,ValueTask_TResult__,Func_TResult,IAction_,Func_Exception,IAction_).md#R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TAction 'R3dux.CustomOperators.InvokeService<TAction,TResult>(this R3.Observable<TAction>, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>>, System.Func<TResult,R3dux.IAction>, System.Func<System.Exception,R3dux.IAction>).TAction')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Threading.Tasks.ValueTask&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Threading.Tasks.ValueTask-1 'System.Threading.Tasks.ValueTask`1')[TResult](CustomOperators.InvokeService_TAction,TResult_(thisObservable_TAction_,Func_TAction,ValueTask_TResult__,Func_TResult,IAction_,Func_Exception,IAction_).md#R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TResult 'R3dux.CustomOperators.InvokeService<TAction,TResult>(this R3.Observable<TAction>, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>>, System.Func<TResult,R3dux.IAction>, System.Func<System.Exception,R3dux.IAction>).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Threading.Tasks.ValueTask-1 'System.Threading.Tasks.ValueTask`1')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A function that performs the service call.

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).successSelector'></a>

`successSelector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](CustomOperators.InvokeService_TAction,TResult_(thisObservable_TAction_,Func_TAction,ValueTask_TResult__,Func_TResult,IAction_,Func_Exception,IAction_).md#R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).TResult 'R3dux.CustomOperators.InvokeService<TAction,TResult>(this R3.Observable<TAction>, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>>, System.Func<TResult,R3dux.IAction>, System.Func<System.Exception,R3dux.IAction>).TResult')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A function that selects the success action based on the result.

<a name='R3dux.CustomOperators.InvokeService_TAction,TResult_(thisR3.Observable_TAction_,System.Func_TAction,System.Threading.Tasks.ValueTask_TResult__,System.Func_TResult,R3dux.IAction_,System.Func_System.Exception,R3dux.IAction_).errorSelector'></a>

`errorSelector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Exception](https://docs.microsoft.com/en-us/dotnet/api/System.Exception 'System.Exception')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A function that selects the error action based on the exception.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence of actions resulting from the service call.