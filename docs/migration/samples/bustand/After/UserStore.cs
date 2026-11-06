using Ducky;

namespace Bustand.After;

#region after-store
// Ducky 2.0: a SliceStore has no constructor dependencies; Set and SetAsync take a mutator that runs at reduce time.
public sealed record User(string Name);

public sealed record UserState(User? User, bool IsLoading = false);

public interface IUserService
{
    Task<User> GetCurrentUserAsync(CancellationToken cancellationToken);
}

public sealed class UserStore : SliceStore<UserState>
{
    protected override UserState Initial => new(User: null, IsLoading: true);

    public void Rename(string name) => Set(state => state with { User = new User(name) });

    public Task<DispatchResult> SignOutAsync() => SetAsync(_ => new UserState(User: null)); // SetAsync(TState) => SetAsync(_ => value)

    // What the effect calls: awaiting the task gives read-your-writes.
    public Task<DispatchResult> SetUserAsync(User u) => SetAsync(_ => new(u));
}
#endregion

#region after-effect
// Ducky 2.0: InitializeAsync becomes an Effect<StoreInitialized> that injects the service and the SliceStore.
public sealed class LoadUser(IUserService users, UserStore store) : Effect<StoreInitialized>
{
    public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken) =>
        await store.SetUserAsync(await users.GetCurrentUserAsync(cancellationToken));
}
#endregion
