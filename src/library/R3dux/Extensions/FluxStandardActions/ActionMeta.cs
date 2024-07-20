namespace R3dux;

/// <summary>
/// Represents the metadata properties of an action.
/// </summary>
/// <param name="TimeStamp">The time the action was created.</param>
public sealed record ActionMeta(DateTime TimeStamp)
{
    /// <summary>
    /// Creates a new instance of the <see cref="ActionMeta"/> record.
    /// </summary>
    /// <returns>A new instance of the <see cref="ActionMeta"/> record.</returns>
    public static ActionMeta Create()
        => new(DateTime.UtcNow);
}