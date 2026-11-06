using System.Collections.Immutable;
using Ducky.Draft;

namespace TodoApp;

#region after-state
[Draftable]
public partial record Todo(int Id, string Title, bool Done);

[Draftable]
public partial record Owner(string Name);

[Draftable]
public partial record TodoState(
    ImmutableList<Todo> Items,
    ImmutableList<string> Tags,
    ImmutableQueue<string> Pending,
    Owner Owner,
    string Filter);
#endregion

public static class TodoReducers
{
    #region after-edits
    public static TodoState SetFilter(TodoState state, string filter) =>
        state.Produce(d => d.Filter = filter);

    public static TodoState RenameOwner(TodoState state, string name) =>
        state.Produce(d => d.Owner.Name = name);
    #endregion

    #region after-lists
    // Ducky.Draft exposes ImmutableList<Todo> as a lazy ListDraft<Todo, Todo.Draft>: LINQ instead of Find.
    public static TodoState Toggle(TodoState state, int id) =>
        state.Produce(d =>
        {
            var todo = d.Items.FirstOrDefault(x => x.Id == id);
            if (todo is not null)
            {
                todo.Done = !todo.Done;
            }
        });

    public static TodoState AddTodo(TodoState state, Todo todo) =>
        state.Produce(d => d.Items.Add(todo));

    public static TodoState RemoveDone(TodoState state) =>
        state.Produce(d => d.Items.RemoveAll(x => x.Done));

    public static TodoState ReplaceTags(TodoState state, IEnumerable<string> tags) =>
        state.Produce(d => d.Tags = tags.ToImmutableList());
    #endregion

    #region after-queue
    // ImmutableQueue<string> stays an ImmutableQueue<string>: assign the result (DUCKY109 flags a discarded one).
    public static TodoState Enqueue(TodoState state, string job) =>
        state.Produce(d => d.Pending = d.Pending.Enqueue(job));
    #endregion

    #region after-steps
    public static TodoState RenameInSteps(TodoState state, int index, string title)
    {
        TodoState.Draft draft = state.CreateDraft();
        draft.Items[index].Title = title;
        return draft.Build();
    }
    #endregion
}
