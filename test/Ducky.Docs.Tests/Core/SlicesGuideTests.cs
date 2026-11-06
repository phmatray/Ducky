using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Docs.Tests.Core;

#region slice-settings
public sealed record Settings(string Theme, int FontSize);

[ActionType("settings/theme-changed")]
public sealed record ThemeChanged(string Theme);

public sealed record SettingsReset;

public sealed class SettingsSlice : Slice<Settings> // key "settings"
{
    public SettingsSlice()
    {
        On<ThemeChanged>((state, action) => state.Theme == action.Theme ? state : state with { Theme = action.Theme });
        On<SettingsReset>(_ => Initial);
    }

    protected override Settings Initial => new("light", FontSize: 14);
}
#endregion

#region slice-pinned-key
public sealed record Layout(bool SidebarOpen);

public sealed record SidebarToggled;

public sealed class LayoutSlice : Slice<Layout>
{
    public LayoutSlice() => On<SidebarToggled>(state => state with { SidebarOpen = !state.SidebarOpen });

    public override string Key => "app-layout"; // pinned: renaming the class keeps the key

    protected override Layout Initial => new(SidebarOpen: true);
}
#endregion

// The snippets of docs/guides/core/slices-and-keys.md.
public sealed class SlicesGuideTests
{
    // Non-normative (M15-02): registration takes the slice type, reads take the state type or the key.
    [Fact]
    public async Task Slices_RegisterAndRead_ByStateTypeAndKey()
    {
        #region slice-register-read
        await using var services = new ServiceCollection()
            .AddDucky(ducky => ducky.AddSlice<SettingsSlice>().AddSlice<LayoutSlice>())
            .BuildServiceProvider();
        var store = services.GetRequiredService<IStore>();

        await store.DispatchAsync(new ThemeChanged("dark"));

        var theme = store.State.Get<Settings>().Theme;               // "dark"
        var layout = (Layout)store.State.Get("app-layout");          // by key
        var settingsSlice = services.GetRequiredService<SettingsSlice>(); // the store-owned instance
        #endregion

        theme.ShouldBe("dark");
        layout.SidebarOpen.ShouldBeTrue();
        store.Slices.ShouldContain(settingsSlice);
        store.State.Keys.ShouldBe(["settings", "app-layout"], ignoreOrder: true);
    }

    // Non-normative (M15-02): the derived key strips "Slice"; an overridden Key pins it.
    [Fact]
    public void Slices_Keys_DerivedOrPinned()
    {
        new SettingsSlice().Key.ShouldBe("settings");
        new LayoutSlice().Key.ShouldBe("app-layout");
    }

    // Non-normative (M15-02): returning the same reference commits nothing; a new one commits and bumps the version.
    [Fact]
    public async Task Slices_SameTheme_NothingCommitted()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<SettingsSlice>());
        await store.DispatchAsync(new ThemeChanged("dark"));
        var before = store.State;

        await store.DispatchAsync(new ThemeChanged("dark"));
        var same = store.State;
        await store.DispatchAsync(new SettingsReset());

        same.ShouldBeSameAs(before);
        store.State.Version.ShouldBe(before.Version + 1);
        store.State.Get<Settings>().ShouldBe(new Settings("light", FontSize: 14));
        store.Failures.ShouldBeEmpty();
    }

    // Non-normative (M15-02): [ActionType] names the action; without it the name is Namespace.Name.
    [Fact]
    public async Task Slices_ActionType_ReadableName()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<SettingsSlice>().AddSlice<LayoutSlice>());

        await store.DispatchAsync(new ThemeChanged("dark"));
        await store.DispatchAsync(new SidebarToggled());

        store.Processed.Where(context => context.Origin == Origin.Local).Select(context => context.ActionType).ShouldBe(["settings/theme-changed", "Ducky.Docs.Tests.Core.SidebarToggled"]);
        store.State.Get<Layout>().SidebarOpen.ShouldBeFalse();
    }
}
