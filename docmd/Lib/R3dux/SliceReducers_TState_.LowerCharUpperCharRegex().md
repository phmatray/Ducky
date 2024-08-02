#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.LowerCharUpperCharRegex() Method

```csharp
private static System.Text.RegularExpressions.Regex LowerCharUpperCharRegex();
```

#### Returns
[System.Text.RegularExpressions.Regex](https://docs.microsoft.com/en-us/dotnet/api/System.Text.RegularExpressions.Regex 'System.Text.RegularExpressions.Regex')

### Remarks
Pattern:<br/>  
  
```csharp  
([a-z])([A-Z])  
```<br/>  
Options:<br/>  
  
```csharp  
RegexOptions.Compiled  
```<br/>  
Explanation:<br/>  
  
```csharp  
○ 1st capture group.<br/>  
    ○ Match a character in the set [a-z].<br/>  
○ 2nd capture group.<br/>  
    ○ Match a character in the set [A-Z].<br/>  
```