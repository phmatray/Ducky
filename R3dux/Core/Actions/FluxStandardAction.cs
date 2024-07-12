namespace R3dux;

/// <summary>
/// A Flux Standard action with optional payload and metadata properties.
/// </summary>
public abstract record FluxStandardAction<TPayload, TMeta> : IAction
{
    /// <summary>
    /// The `type` of an action identifies to the consumer the nature of the action that has occurred.
    /// </summary>
    public abstract string TypeKey { get; init; }
    
    /// <summary>
    /// The optional `payload` property MAY be any type of value.
    /// It represents the payload of the action.
    /// Any information about the action that is not the type or status of the action should be part of the `payload` field.
    /// By convention, if `error` is `true`, the `payload` SHOULD be an <see cref="Exception"/> object.
    /// This is akin to rejecting an asynchronous operation with an <see cref="Exception"/> object.
    /// </summary>
    public virtual TPayload? Payload { get; init; }

    /// <summary>
    /// The optional `error` property MAY be set to true if the action represents an error.
    /// An action whose `error` is true is analogous to a rejected asynchronous operation.
    /// By convention, the `payload` SHOULD be an <see cref="Exception"/> object.
    /// If `error` has any other value besides `true`, including `null`, the action MUST NOT be interpreted as an error.
    /// </summary>
    public virtual bool? Error { get; init; }

    /// <summary>
    /// The optional `meta` property MAY be any type of value.
    /// It is intended for any extra information that is not part of the payload.
    /// </summary>
    public virtual TMeta? Meta { get; init; }
}

/// <summary>
/// A Flux Standard action with a generic payload and metadata type.
/// </summary>
public abstract record FluxStandardAction
    : FluxStandardAction<object, object>;