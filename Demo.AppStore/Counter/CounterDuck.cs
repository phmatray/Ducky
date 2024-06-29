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
