#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux')

## MergeStrategy Enum

Defines strategies for merging entities into the state.

```csharp
public enum MergeStrategy
```
### Fields

<a name='R3dux.MergeStrategy.FailIfDuplicate'></a>

`FailIfDuplicate` 0

Fail if a duplicate entity is found during the merge.

<a name='R3dux.MergeStrategy.Overwrite'></a>

`Overwrite` 1

Overwrite existing entities with the same key during the merge.