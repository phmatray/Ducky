using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace Bustand.After;

#region after-component
// Ducky 2.0: Select in OnInitialized replaces UseState; the component re-renders when the selected value changes.
public sealed class UserBadge : DuckyComponent
{
    private Selection<string>? _name;

    protected override void OnInitialized() => _name = Select((UserState state) => state.User?.Name ?? "guest");

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, _name!.Value);
}
#endregion
