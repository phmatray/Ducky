namespace Demo.AppStore;

public interface IFsaAction<out TPayload>
    : IAction
{
    string Type { get; }
    TPayload? Payload { get; }
    string? Error { get; }
    object? Meta { get; }
}

public abstract record FsaAction<TPayload>(
    TPayload? Payload = default,
    string? Error = null,
    object? Meta = null)
    : IFsaAction<TPayload>
{
    public virtual string Domain
        => GetType().Namespace ?? string.Empty;
    
    public virtual string Type
        => $"[{Domain}] {GetType().Name}";
    
    public bool HasError
        => !string.IsNullOrEmpty(Error);
}

public record FsaAction
    : FsaAction<object?>;

// Actions
public record Increment : FsaAction;

public record Decrement : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

// Reducer
public class CounterReducer : Reducer<int>
{
    private const int InitialState = 10;

    public override int Reduce(int state, IAction action)
        => action switch
        {
            Increment => state + 1,
            Decrement => state - 1,
            Reset => InitialState,
            SetValue setValueAction => setValueAction.Value,
            _ => state
        };
}

// Effects
public class IncrementEffect : Effect<int>
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<int> state)
    {
        // if the Value is greater than 15, then reset the counter
        return actions
            .FilterActions<Increment>()
            .WithLatestFrom(state, (action, stateValue) => new { action, stateValue })
            .Where(x => x.stateValue > 15)
            .Delay(TimeSpan.FromSeconds(1))
            .SelectAction(x => new Reset());
    }
}
