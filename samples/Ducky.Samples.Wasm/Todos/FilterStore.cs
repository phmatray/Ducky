namespace Ducky.Samples.Wasm.Todos;

public enum TodoView
{
    All,
    Active,
    Done,
}

public sealed record TodoFilter(TodoView View);

// A slice with methods: components call Filter.Show(view) instead of dispatching; DevTools names the change filter/Show.
public sealed class FilterStore : SliceStore<TodoFilter>
{
    protected override TodoFilter Initial => new(TodoView.All);

    public void Show(TodoView view) => Set(state => state with { View = view });
}
