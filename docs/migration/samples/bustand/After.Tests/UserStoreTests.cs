using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Bustand.After.Tests;

internal sealed class FakeUserService(Task<User> user) : IUserService
{
    public CancellationToken Token { get; private set; }

    public Task<User> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        Token = cancellationToken;
        return user;
    }
}

// The migrated UserStore keeps the Bustand store's behaviour; the load moved to the LoadUser effect (SPEC §22.2).
public sealed class UserStoreTests
{
    [Fact]
    public async Task LoadUser_AtInit_SetsUserThroughSliceStore()
    {
        var fake = new FakeUserService(Task.FromResult(new User("Ada")));
        UserStore? users = null;

        #region after-test
        var run = await EffectTest.Run<LoadUser, StoreInitialized>(
            new StoreInitialized(),
            ducky => ducky.AddSlice<UserStore>(),
            store => users = store.GetSlice<UserStore>(),
            services: s => s.AddSingleton<IUserService>(fake));
        #endregion

        users!.State.ShouldBe(new UserState(new User("Ada")));
        run.Dispatched.Count.ShouldBe(1); // the SetState of SetUserAsync
        run.Failures.ShouldBeEmpty();
        fake.Token.CanBeCanceled.ShouldBeTrue(); // the effect passed its run token
    }

    [Fact]
    public async Task Initial_IsLoading()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<UserStore>());

        store.GetSlice<UserStore>().State.ShouldBe(new UserState(User: null, IsLoading: true));
    }

    [Fact]
    public async Task Rename_SetsUserKeepsLoading()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<UserStore>());
        var users = store.GetSlice<UserStore>();

        users.Rename("Grace");
        await store.Settled();

        users.State.ShouldBe(new UserState(new User("Grace"), IsLoading: true));
        store.Processed.Select(context => context.ActionType).ShouldContain("user/Rename");
    }

    [Fact]
    public async Task SignOutAsync_ClearsUser_Reduced()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<UserStore>());
        var users = store.GetSlice<UserStore>();
        store.Seed(new UserState(new User("Ada")));

        var result = await users.SignOutAsync();

        result.ShouldBe(DispatchResult.Reduced);
        users.State.ShouldBe(new UserState(User: null)); // read-your-writes: no Settled needed
    }
}

public sealed class UserBadgeTests : BunitContext
{
    private readonly TaskCompletionSource<User> _user = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public UserBadgeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Registration.AddApp(Services, new FakeUserService(_user.Task));
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    [Fact]
    public void Load_RerendersSelectedName()
    {
        var badge = Render<UserBadge>();
        badge.Markup.ShouldBe("guest");

        _user.SetResult(new User("Ada"));

        badge.WaitForAssertion(() => badge.Markup.ShouldBe("Ada"));
    }
}
