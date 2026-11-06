using System.Collections.Immutable;

namespace Ducky.Samples.Wasm.Todos;

public static class TodoSelectors
{
    public static readonly SelectorDef<ImmutableList<Todo>> Visible = Selector.Define(
        static s => s.Get<TodoList>().Items,
        static s => s.Get<TodoFilter>().View,
        static (items, view) => view switch
        {
            TodoView.Active => items.RemoveAll(static todo => todo.Done),
            TodoView.Done => items.RemoveAll(static todo => !todo.Done),
            _ => items,
        });

    public static readonly SelectorDef<int> Remaining = Selector.Define(
        static s => s.Get<TodoList>().Items,
        static items => items.Count(static todo => !todo.Done));
}
