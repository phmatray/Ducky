namespace R3dux;

/// <summary>
/// A Flux Standard action with an error property.
/// </summary>
/// <param name="Payload">The error payload of the action.</param>
public abstract record FsaError(Exception Payload)
    : Fsa, IFsaPayload<Exception>
{
    /// <summary>
    /// The error payload of the action.
    /// </summary>
    public Exception Payload { get; init; } = Payload;
    
    /// <summary>
    /// The error property is true if the action represents an error.
    /// </summary>
    public bool Error => true;
}

/// <summary>
/// A Flux Standard action with an error property and metadata properties.
/// </summary>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public abstract record FsaError<TMeta>(Exception Payload, TMeta Meta)
    : FsaError(Payload), IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}