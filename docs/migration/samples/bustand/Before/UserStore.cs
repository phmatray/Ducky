using Bustand.Attributes;
using Bustand.Core;

namespace Bustand.Before;

#region before-store
// Bustand: a ZustandStore injects its services and loads in InitializeAsync, then sets state.
public sealed record User(string Name);

public sealed record UserState(User? User, bool IsLoading = false);

public interface IUserService
{
    Task<User> GetCurrentUserAsync();
}

[BustandStore]
public sealed class UserStore : ZustandStore<UserState>
{
    private readonly IUserService _userService;

    public UserStore(IUserService userService) => _userService = userService;

    protected override UserState InitialState => new(User: null, IsLoading: true);

    public void Rename(string name) => Set(state => state with { User = new User(name) });

    public Task SignOutAsync() => SetAsync(new UserState(User: null));

    protected override async Task InitializeAsync()
    {
        var user = await _userService.GetCurrentUserAsync();
        await SetAsync(state => state with { User = user, IsLoading = false });
    }
}
#endregion
