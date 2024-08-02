#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[R3duxException](R3duxException.md 'R3dux.R3duxException')

## R3duxException(string, Exception) Constructor

Initializes a new instance of the [R3duxException](R3duxException.md 'R3dux.R3duxException') class with a specified error message and a reference to the inner exception that is the cause of this exception.

```csharp
public R3duxException(string message, System.Exception innerException);
```
#### Parameters

<a name='R3dux.R3duxException.R3duxException(string,System.Exception).message'></a>

`message` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The message that describes the error.

<a name='R3dux.R3duxException.R3duxException(string,System.Exception).innerException'></a>

`innerException` [System.Exception](https://docs.microsoft.com/en-us/dotnet/api/System.Exception 'System.Exception')

The exception that is the cause of the current exception.