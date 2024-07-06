namespace Demo.AppStore;

// Actions
public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

// Reducers
public class CounterReducers : ReducerCollection<int>
{
    private const int InitialState = 10;

    public CounterReducers()
    {
        Map<Increment>((state, _) => state + 1);
        Map<Decrement>((state, _) => state - 1);
        Map<Reset>((_, _) => InitialState);
        Map<SetValue>((_, action) => action.Value);
    }
}

// Effects
public class IncrementEffect : Effect
{
    // public override Observable<object> Handle(
    //     Observable<object> actions, Observable<int> state)
    // {
    //     // if the Value is greater than 15, then reset the counter
    //     return actions
    //         .FilterActions<Increment>()
    //         .WithLatestFrom(state, (action, stateValue) => new { action, stateValue })
    //         .Where(x => x.stateValue > 15)
    //         .Delay(TimeSpan.FromSeconds(3))
    //         .SelectAction(x => new Reset());
    // }

    public override Observable<IAction> Handle(
        Observable<IAction> actions, Store store)
    {
        // if the Value is greater than 15, then reset the counter
        return actions
            .FilterActions<Increment>()
            .Where(x => store.GetState<int>("counter") > 15)
            // TODO: Implement WithSliceState
            // .WithSliceState<CounterState>()
            // .Where((sliceState, action) => sliceState > 15)
            .Delay(TimeSpan.FromSeconds(3))
            .SelectAction(x => new Reset());
    }
}

// Slice
public record CounterSlice : Slice<int>
{
    public override string Key => "counter";
    public override int InitialState => 10;
    public override ReducerCollection<int> Reducers { get; } = new CounterReducers();
}
