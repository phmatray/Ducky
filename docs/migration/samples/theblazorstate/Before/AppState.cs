using TheBlazorState.Attributes;

namespace TheBlazorState.Before;

#region before-shared
// TheBlazorState: a [Shared] POCO registered in DI; every component that injects it re-renders when a property changes.
public sealed partial class AppState
{
    [Shared]
    public partial string? UserName { get; set; }
}
#endregion
