namespace R3dux;

/// <summary>
/// A Flux Standard action with metadata properties.
/// </summary>
/// <param name="Meta">The metadata of the action.</param>
/// <typeparam name="TMeta"></typeparam>
public abstract record FsaMeta<TMeta>(TMeta Meta)
    : Fsa, IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}