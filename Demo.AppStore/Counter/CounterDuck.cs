﻿namespace Demo.AppStore;

#region Actions

public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;
public record SetValue(int Value) : IAction;

#endregion

#region Reducers

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

    public override string GetKey() => "counter";
    
    public override int GetInitialState() => 10;
}

#endregion
