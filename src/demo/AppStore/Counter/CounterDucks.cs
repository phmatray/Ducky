namespace AppStore;

#region Actions

public record Increment(int Amount = 1) : IAction;
public record Decrement(int Amount = 1) : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

#endregion

#region Reducers

public record CounterReducers : SliceReducers<int>
{
    public CounterReducers()
    {
        Map<Increment>((state, action) => state + action.Amount);
        Map<Decrement>((state, action) => state - action.Amount);
        Map<Reset>(GetInitialState);
        Map<SetValue>((_, action) => action.Value);
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