// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

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

public class CounterEffects : IAsyncEffect<Increment>, IAsyncEffect<Decrement>
{
    public async Task HandleAsync(Increment action, IRootState rootState, IDispatcher dispatcher)
    {
        const int threshold = 15;
        const int delayInSeconds = 3;

        CounterState counterState = rootState.GetSliceState<CounterState>();

        // if the Value is greater than 15, then reset the counter
        if (counterState.Value > threshold)
        {
            await Task.Delay(TimeSpan.FromSeconds(delayInSeconds), ObservableSystem.DefaultTimeProvider);
            dispatcher.Dispatch(new Reset());
        }

        await Task.CompletedTask;
    }

    public async Task HandleAsync(Decrement action, IRootState rootState, IDispatcher dispatcher)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        // if the Value is smaller than 0, then reset it to 5
        if (counterState.Value < 0)
        {
            dispatcher.Dispatch(new SetValue(5));
        }

        await Task.CompletedTask;
    }
}

#endregion
