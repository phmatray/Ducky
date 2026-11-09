namespace Ducky.Playground.Xray;

public sealed record XrayView(bool On, bool InspectorOpen);

// A SliceStore: a slice with methods. The switch calls Xray.Toggle() instead of dispatching an action; the store still
// reduces the change (DevTools and the inspector name it xray/Toggle) and persists it, so X-ray remembers its state.
public sealed class XrayStore : SliceStore<XrayView>
{
    public override string Key => "xray";

    protected override XrayView Initial => new(On: false, InspectorOpen: false);

    public void Toggle() => Set(view => view with { On = !view.On });

    public void ToggleInspector() => Set(view => view with { InspectorOpen = !view.InspectorOpen });
}

// The code drawer: which feature is open, which of its files is shown, and where focus goes back when it closes.
public sealed record DrawerView(string? Feature, int File, string? ReturnTo);

public sealed class DrawerStore : SliceStore<DrawerView>
{
    public override string Key => "drawer";

    protected override DrawerView Initial => new(Feature: null, File: 0, ReturnTo: null);

    public void Toggle(string feature) => Set(view => view.Feature == feature
        ? new DrawerView(null, 0, feature)
        : new DrawerView(feature, 0, null));

    public void ShowFile(int file) => Set(view => view.File == file ? view : view with { File = file });

    public void Close() => Set(view => view.Feature is null ? view : new DrawerView(null, 0, view.Feature));
}
