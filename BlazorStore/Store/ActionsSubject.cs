namespace BlazorStore;

public class ActionsSubject : IDisposable
{
    private readonly BehaviorSubject<IAction> _subject = new(new InitAction());

    public IObservable<IAction> Actions
        => _subject.AsObservable();

    public void OnNext(IAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Type);

        _subject.OnNext(action);
    }

    public void OnError(Exception error)
    {
        _subject.OnError(error);
    }

    public void OnCompleted()
    {
        _subject.OnCompleted();
    }

    public void Dispose()
    {
        _subject.OnCompleted();
        _subject.Dispose();
    }
}