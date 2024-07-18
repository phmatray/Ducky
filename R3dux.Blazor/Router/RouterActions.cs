namespace R3dux.Blazor.Router;

// original doc: https://ngrx.io/guide/router-store/actions
// original src: https://github.com/ngrx/platform/tree/main/modules/router-store/src

/// <summary>
/// An action dispatched when a router navigation request is fired.
/// </summary>
public sealed record RouterRequestAction(RouterRequestAction.ActionPayload Payload)
    : Fsa<RouterRequestAction.ActionPayload>(Payload)
{
    public sealed record ActionPayload(object RouterState, object Event);
    
    public override string TypeKey => "router-store/request";
}

/// <summary>
/// An action dispatched when the router navigates.
/// </summary>
public sealed record RouterNavigationAction : Fsa
{
    public override string TypeKey => "router-store/navigation";
}

/// <summary>
/// An action dispatched after navigation has ended and new route is active.
/// </summary>
public sealed record RouterNavigatedAction : Fsa
{
    public override string TypeKey => "router-store/navigated";
}

/// <summary>
/// An action dispatched when the router cancels navigation.
/// </summary>
public sealed record RouterCancelAction : Fsa
{
    public override string TypeKey => "router-store/cancel";
}

/// <summary>
/// An action dispatched when the router errors.
/// </summary>
public sealed record RouterErrorAction : Fsa
{
    public override string TypeKey => "router-store/error";
}