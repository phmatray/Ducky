using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests.Core;

#region testing-effect-actions
public sealed record Profile(int Id, string Name);

public sealed record LoadProfile(int Id);

public sealed record ProfileLoaded(Profile Profile);

public interface IProfileApi
{
    Task<Profile> GetAsync(int id, CancellationToken cancellationToken);
}
#endregion

// The snippets of docs/guides/core/testing.md: each region is a test of its own.
public sealed class TestingGuideTests
{
    // Private: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every accessible effect of this assembly.
    private static class App
    {
        #region testing-effect-under-test
        public sealed class LoadProfileEffect(IProfileApi api) : Effect<LoadProfile>
        {
            public override async Task Handle(LoadProfile action, EffectContext context, CancellationToken cancellationToken) =>
                context.Dispatch(new ProfileLoaded(await api.GetAsync(action.Id, cancellationToken)));
        }
        #endregion
    }

    #region testing-slice
    [Fact]
    public void ThemeChanged_NewTheme_Applied() =>
        SliceTest.For<SettingsSlice, Settings>()
            .Given(new Settings("light", FontSize: 14))
            .When(new ThemeChanged("dark"))
            .Then(new Settings("dark", FontSize: 14));

    [Fact]
    public void ThemeChanged_SameTheme_Unchanged() =>
        SliceTest.For<SettingsSlice, Settings>()
            .Given(new Settings("dark", FontSize: 14))
            .When(new ThemeChanged("dark"))
            .ThenUnchanged();
    #endregion

    // Non-normative (M15-02): a wrong expectation fails with DuckyAssertionException, whatever the test framework.
    [Fact]
    public void SliceTest_WrongExpectation_ThrowsDuckyAssertionException() =>
        Should.Throw<DuckyAssertionException>(() => SliceTest.For<SettingsSlice, Settings>()
            .Given(new Settings("light", FontSize: 14))
            .When(new ThemeChanged("dark"))
            .ThenUnchanged());

    #region testing-effect
    [Fact]
    public async Task LoadProfile_ApiAnswers_DispatchesProfileLoaded()
    {
        var run = await EffectTest.Run<App.LoadProfileEffect, LoadProfile>(
            new LoadProfile(42),
            services: services => services.AddSingleton<IProfileApi>(new StubProfileApi(new Profile(0, "Ada"))));

        run.Dispatched.ShouldBe([new ProfileLoaded(new Profile(42, "Ada"))]);
        run.Failures.ShouldBeEmpty();
    }
    #endregion

    #region testing-store
    [Fact]
    public async Task TestStore_SeedDispatchSettle()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>().AddSlice<CartStore>());
        store.Seed(new Todos(EntityState<int, TodoItem>.Empty.Add(new TodoItem(1, "Write docs", Done: false))));

        store.Dispatch(new TodoToggled(1));
        store.GetSlice<CartStore>().Add(new CartLine("duck-yellow", Quantity: 1));
        await store.Settled();

        store.State.WasRestored<Todos>().ShouldBeTrue();
        store.State.Get<Todos>().Items.Find(1)!.Done.ShouldBeTrue();
        store.State.Get<Cart>().Lines.Count.ShouldBe(1);
        store.Processed.Where(context => context.Origin == Origin.Local).Select(context => context.ActionType)
            .ShouldBe(["Ducky.Docs.Tests.Core.TodoToggled", "cart/Add"]);
        store.Failures.ShouldBeEmpty();
    }
    #endregion

    // Non-normative (M15-02): TestStore is strict: an action nothing handles is a failure.
    [Fact]
    public async Task TestStore_UnhandledAction_IsAFailure()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>());

        await store.DispatchAsync(new LoadProfile(42));

        store.Failures.ShouldHaveSingleItem().ShouldBeOfType<ReducerFailed>().Exception.ShouldBeOfType<UnhandledActionException>();
    }

    private sealed class StubProfileApi(Profile profile) : IProfileApi
    {
        public async Task<Profile> GetAsync(int id, CancellationToken cancellationToken)
        {
            await Task.Yield(); // a real API answers later
            return profile with { Id = id };
        }
    }
}
