namespace R3dux;

/// <summary>
/// Represents an action with a payload property.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public interface IFsaPayload<TPayload>
{
    /// <summary>
    /// The optional `payload` property MAY be any type of value.
    /// </summary>
    TPayload Payload { get; init; }
}