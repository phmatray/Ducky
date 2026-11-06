using System.Collections.Immutable;
using Mutty;

namespace TodoApp;

#region before-state
[MutableGeneration]
public record Todo(int Id, string Title, bool Done);

[MutableGeneration]
public record Owner(string Name);

[MutableGeneration]
public record TodoState(
    ImmutableList<Todo> Items,
    ImmutableList<string> Tags,
    ImmutableQueue<string> Pending,
    Owner Owner,
    string Filter);
#endregion

public static class TodoReducers
{
    #region before-edits
    public static TodoState SetFilter(TodoState state, string filter) =>
        state.Produce(d => d.Filter = filter);

    public static TodoState RenameOwner(TodoState state, string name) =>
        state.Produce(d => d.Owner.Name = name);
    #endregion

    #region before-lists
    // Mutty exposes ImmutableList<Todo> as a List<MutableTodo>.
    public static TodoState Toggle(TodoState state, int id) =>
        state.Produce(d =>
        {
            var todo = d.Items.Find(x => x.Id == id);
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
        state.Produce(d => d.Tags = new List<string>(tags));
    #endregion

    #region before-queue
    // Mutty exposes ImmutableQueue<string> as a mutable Queue<string>.
    public static TodoState Enqueue(TodoState state, string job) =>
        state.Produce(d => d.Pending.Enqueue(job));
    #endregion

    #region before-steps
    public static TodoState RenameInSteps(TodoState state, int index, string title)
    {
        MutableTodoState draft = state.CreateDraft();
        draft.Items[index].Title = title;
        return draft.FinishDraft();
    }
    #endregion
}
