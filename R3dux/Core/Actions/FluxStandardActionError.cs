namespace R3dux;

/// <summary>
/// An extension of the Flux Standard action that represents an action containing an error as its payload.
/// </summary>
/// <typeparam name="TError">The type of the error payload.</typeparam>
public abstract record FluxStandardActionError<TError>
    : FluxStandardAction<TError, object>
    where TError : Exception
{
    // The required `error` property MUST be set to `true` if the action represents an error.
    public override bool? Error => true;
    
    // Don't expose Meta for error actions.
    private new object? Meta => null;
}