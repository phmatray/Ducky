using Bustand.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Bustand.Before;

#region before-component
// Bustand: UseState subscribes to a selected value of the injected store.
public sealed class UserBadge : ZustandComponent<UserStore, UserState>
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
        builder.AddContent(0, UseState(state => state.User?.Name ?? "guest").Value);
}
#endregion
