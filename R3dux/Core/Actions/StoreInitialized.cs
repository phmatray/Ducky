namespace R3dux;

/// <summary>
/// Represents an action that is dispatched when the store is initialized.
/// </summary>
public sealed record StoreInitialized
    : FluxStandardAction
{
    /// <inheritdoc />
    public override string TypeKey { get; init; } = "store/initialized";
}
