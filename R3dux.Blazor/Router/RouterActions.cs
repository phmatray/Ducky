namespace R3dux.Blazor.Router;

// see: https://ngrx.io/guide/router-store/actions

// routerRequestAction
// At the start of each navigation, the router will dispatch a ROUTER_REQUEST action.

// routerNavigationAction
// During navigation, before any guards or resolvers run, the router will dispatch a ROUTER_NAVIGATION action.
// If you want the ROUTER_NAVIGATION to be dispatched after guards or resolvers run, change the Navigation Action Timing.

// routerNavigatedAction
// After a successful navigation, the router will dispatch a ROUTER_NAVIGATED action.

// routerCancelAction
// When the navigation is cancelled, for example due to a guard saying that the user cannot access the requested page, the router will dispatch a ROUTER_CANCEL action.
// The action contains the store state before the navigation. You can use it to restore the consistency of the store.

// routerErrorAction
// When there is an error during navigation, the router will dispatch a ROUTER_ERROR action.
// The action contains the store state before the navigation. You can use it to restore the consistency of the store.