namespace R3dux;

/// <summary>
/// Represents an action with a metadata property.
/// </summary>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public interface IFsaMeta<TMeta>
{
    /// <summary>
    /// The optional `meta` property MAY be any type of value.
    /// </summary>
    TMeta Meta { get; init; }
}
