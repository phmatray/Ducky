// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Counter;

#region State

public record CounterState(int Value);

#endregion

#region Actions

public record Increment(int Amount = 1) : IAction;

public record Decrement(int Amount = 1) : IAction;

public record Reset : IAction;

public record SetValue(int Value) : IAction;

#endregion

#region Reducers

public record CounterReducers : SliceReducers<CounterState>
{
    public CounterReducers()
    {
        On<Increment>((state, action) => new(state.Value + action.Amount));
        On<Decrement>((state, action) => new(state.Value - action.Amount));
        On<Reset>(GetInitialState);
        On<SetValue>((_, action) => new(action.Value));
    }

    public override CounterState GetInitialState()
    {
        return new(10);
    }
}

#endregion

#region Effects

// public class IncrementEffect : ReactiveEffect
// {
//     public override Observable<IAction> Handle(
//         Observable<IAction> actions,
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

public class ResetCounterAfter3Sec : Effect<Increment>
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
