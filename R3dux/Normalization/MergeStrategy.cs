namespace R3dux.Normalization;

/// <summary>
/// Defines strategies for merging entities into the state.
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// Fail if a duplicate entity is found during the merge.
    /// </summary>
    FailIfDuplicate,

    /// <summary>
    /// Overwrite existing entities with the same key during the merge.
    /// </summary>
    Overwrite,
}