// A SliceStore is a slice with methods: the X-ray switch calls Toggle(), and the store
// still reduces the change (named xray/Toggle) and persists it.
namespace Ducky.Playground.Xray;

public sealed record XrayView(bool On, bool InspectorOpen);

public sealed class XrayStore : SliceStore<XrayView>
{
    public override string Key => "xray";

    protected override XrayView Initial => new(On: false, InspectorOpen: false);

    public void Toggle() => Set(view => view with { On = !view.On });

    public void ToggleInspector() =>
        Set(view => view with { InspectorOpen = !view.InspectorOpen });
}

// The code drawer: the open feature, the index of the file shown, and the feature whose
// tag gets focus back when the drawer closes.
public sealed record DrawerView(string? Feature, int FileIndex, string? ReturnFocusTo);

public sealed class DrawerStore : SliceStore<DrawerView>
{
    public override string Key => "drawer";

    protected override DrawerView Initial =>
        new(Feature: null, FileIndex: 0, ReturnFocusTo: null);

    // Pressing the open feature's tag again closes the drawer.
    public void Toggle(string feature) => Set(view => view.Feature == feature
        ? new DrawerView(Feature: null, FileIndex: 0, ReturnFocusTo: feature)
        : new DrawerView(feature, FileIndex: 0, ReturnFocusTo: null));

    public void ShowFile(int fileIndex) =>
        Set(view => view.FileIndex == fileIndex
            ? view
            : view with { FileIndex = fileIndex });

    public void Close() => Set(view => view.Feature is null
        ? view
        : new DrawerView(Feature: null, FileIndex: 0, ReturnFocusTo: view.Feature));
}
