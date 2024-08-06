#### [R3dux.Selectors](R3dux.Selectors.md 'R3dux.Selectors')
### [R3dux](R3dux.Selectors.md#R3dux 'R3dux').[MemoizedSelector](MemoizedSelector.md 'R3dux.MemoizedSelector')

## MemoizedSelector.AreDependenciesEqual(object[], object[]) Method

Compares two sets of dependencies to determine if they are equal.

```csharp
private static bool AreDependenciesEqual(object[] oldDeps, object[] newDeps);
```
#### Parameters

<a name='R3dux.MemoizedSelector.AreDependenciesEqual(object[],object[]).oldDeps'></a>

`oldDeps` [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The old set of dependencies.

<a name='R3dux.MemoizedSelector.AreDependenciesEqual(object[],object[]).newDeps'></a>

`newDeps` [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The new set of dependencies.

#### Returns
[System.Boolean](https://docs.microsoft.com/en-us/dotnet/api/System.Boolean 'System.Boolean')  
True if the dependencies are equal; otherwise, false.