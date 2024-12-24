// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Counter;

#region State

public record CounterState(int Value);

#endregion

#region Actions

public record Increment(int Amount = 1);

public record Decrement(int Amount = 1);

public record Reset;

public record SetValue(int Value);

#endregion

#region Reducers

public record CounterReducers : SliceReducers<CounterState>
{
    public CounterReducers()
    {
        On<Increment>(Reduce);
        On<Decrement>(Reduce);
        On<Reset>(GetInitialState);
        On<SetValue>(Reduce);
    }

    public override CounterState GetInitialState()
        => new(10);

    private static CounterState Reduce(CounterState state, Increment action)
        => new(state.Value + action.Amount);

    private static CounterState Reduce(CounterState state, Decrement action)
        => new(state.Value - action.Amount);

    private static CounterState Reduce(CounterState _, SetValue action)
        => new(action.Value);
}

#endregion

#region Effects

// public class IncrementEffect : ReactiveEffect
// {
//     public override Observable<object> Handle(
//         Observable<object> actions,
//         Observable<IRootState> rootState)
//     {
//         // if the Value is greater than 15, then reset the counter
//         return actions
//             .OfActionType<Increment>()
//             .WithSliceState<CounterState, Increment>(rootState)
//             .Where(pair => pair.State.Value > 15)
//             .Delay(TimeSpan.FromSeconds(3), TimeProvider)
//             .SelectAction(_ => new Reset());
//     }
// }

public class ResetCounterAfter3Sec : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        // if the Value is greater than 15, then reset the counter
        if (counterState.Value > 15)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ObservableSystem.DefaultTimeProvider);
            Dispatch(new Reset());
        }

        await Task.CompletedTask;
    }
}

#endregion
