namespace R3dux;

/// <summary>
/// A Flux Standard action without payload or metadata properties.
/// </summary>
public abstract record Fsa
    : IAction
{
    /// <summary>
    /// The `type` of an action identifies to the consumer the nature of the action that has occurred.
    /// </summary>
    public abstract string TypeKey { get; }
}

/// <summary>
/// A Flux Standard action with a generic payload type. 
/// </summary>
/// <param name="Payload">The payload of the action.</param>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public abstract record Fsa<TPayload>(TPayload Payload)
    : Fsa, IFsaPayload<TPayload>
{
    /// <inheritdoc />
    public TPayload Payload { get; init; } = Payload;
}

/// <summary>
/// A Flux Standard action with a generic payload and metadata type.
/// </summary>
/// <param name="Payload">The payload of the action.</param>
/// <param name="Meta">The metadata of the action.</param>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public abstract record Fsa<TPayload, TMeta>(TPayload Payload, TMeta Meta)
    : Fsa<TPayload>(Payload), IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}