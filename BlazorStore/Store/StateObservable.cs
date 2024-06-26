namespace BlazorStore;

public abstract class StateObservable<TState> : IObservable<TState>
{
    public abstract IDisposable Subscribe(IObserver<TState> observer);
    public abstract TState Value { get; }
}