namespace R3dux.Blazor.Router;

// original doc: https://ngrx.io/guide/router-store/actions
// original src: https://github.com/ngrx/platform/tree/main/modules/router-store/src

/// <summary>
/// An action dispatched when a router navigation request is fired.
/// </summary>
public sealed record RouterRequestAction
    : FluxStandardAction<RouterRequestAction.ActionPayload, object>
{
    public sealed record ActionPayload(object RouterState, object Event);
    
    public override string TypeKey { get; init; } = "router-store/request";
}

/// <summary>
/// An action dispatched when the router navigates.
/// </summary>
public sealed record RouterNavigationAction
    : FluxStandardAction
{
    public override string TypeKey { get; init; } = "router-store/navigation";
}

/// <summary>
/// An action dispatched after navigation has ended and new route is active.
/// </summary>
public sealed record RouterNavigatedAction
    : FluxStandardAction
{
    public override string TypeKey { get; init; } = "router-store/navigated";
}

/// <summary>
/// An action dispatched when the router cancels navigation.
/// </summary>
public sealed record RouterCancelAction
    : FluxStandardAction
{
    public override string TypeKey { get; init; } = "router-store/cancel";
}

/// <summary>
/// An action dispatched when the router errors.
/// </summary>
public sealed record RouterErrorAction
    : FluxStandardAction
{
    public override string TypeKey { get; init; } = "router-store/error";
}