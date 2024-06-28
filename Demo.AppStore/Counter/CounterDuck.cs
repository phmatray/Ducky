namespace Demo.AppStore;

// Actions
public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

// Reducer
public class CounterReducer : Reducer<int>
{
    private const int InitialState = 10;

    public override int ReduceAction(int state, IAction action)
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
    /// <inheritdoc />
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<int> state)
    {
        return actions
            .OfType<IAction, Increment>()
            .Delay(TimeSpan.FromSeconds(1))
            .WithLatestFrom(state, (action, stateValue) => new { action, stateValue })
            .Select(x => new SetValue(x.stateValue + 1))
            .Cast<SetValue, IAction>();
    }
}
