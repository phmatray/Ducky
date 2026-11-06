using Ducky;

namespace TheBlazorState.After;

#region after-shared
// Ducky 2.0: the [Shared] POCO becomes a SliceStore; its setters are Set methods. Components inject it and Select its state.
public sealed record AppState(string? UserName);

public sealed class AppStore : SliceStore<AppState>
{
    protected override AppState Initial => new(UserName: null);

    public void SetUserName(string? name) => Set(state => state with { UserName = name });
}
#endregion
