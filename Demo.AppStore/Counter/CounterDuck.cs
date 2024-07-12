namespace Demo.AppStore;

#region Actions

public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

#endregion

#region Reducers

public class CounterReducers : ReducerCollection<int>
{
    public CounterReducers()
    {
        Map<Increment>((state, action) => state + 1);
        Map<Decrement>((state, action) => state - 1);
        Map<Reset>((state, action) => GetInitialState());
        Map<SetValue>((state, action) => action.Value);
    }

    public override int GetInitialState()
    {
        return 10;
    }
}

#endregion

#region Effects

public class IncrementEffect : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState)
    {
        // if the Value is greater than 15, then reset the counter
        return actions
            .FilterActions<Increment>()    
            .WithSliceState<int, Increment>(rootState)
            .Where(pair => pair.State > 15)
            .Delay(TimeSpan.FromSeconds(3), TimeProvider)
            .SelectAction(_ => new Reset());
    }
}

#endregion

#region 

public record CounterSlice : Slice<int>
{
    public override ReducerCollection<int> Reducers { get; } = new CounterReducers();
}

#endregion