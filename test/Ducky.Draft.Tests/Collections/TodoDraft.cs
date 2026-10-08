namespace Ducky.Draft.Tests.Collections;

internal sealed record Todo(int Id, string Title);

// An entity record that compares by Id only: an edit through its element draft must still reach the built collection.
internal sealed record Entity(int Id, string Title)
{
    public bool Equals(Entity? other) => other?.Id == Id;

    public override int GetHashCode() => Id;
}

internal sealed class TodoDraft(Todo original) : TitleDraft<Todo>(original, x => x.Title, (x, title) => x with { Title = title });

internal sealed class EntityDraft(Entity original) : TitleDraft<Entity>(original, x => x.Title, (x, title) => x with { Title = title });

// A hand-written stand-in for the generated {R}.Draft (§13.3) until the Draft generator lands (M10-04): Build returns the
// original when the title is reverted, and a revoked draft throws.
internal abstract class TitleDraft<T>(T original, Func<T, string> title, Func<T, string, T> retitle) : IDraft<T>
    where T : class
{
    private string _title = title(original);
    private bool _revoked;

    public string Title
    {
        get
        {
            Guard();
            return _title;
        }
        set
        {
            Guard();
            _title = value;
        }
    }

    public bool IsDirty
    {
        get
        {
            Guard();
            return _title != title(original);
        }
    }

    public T Build()
    {
        Guard();
        return _title == title(original) ? original : retitle(original, _title);
    }

    void IRevocable.Revoke() => _revoked = true;

    private void Guard() => ObjectDisposedException.ThrowIf(_revoked, this);
}
