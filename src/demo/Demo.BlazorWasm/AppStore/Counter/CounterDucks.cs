// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.AppStore;

#region State

public record CounterState(int Value);

#endregion

#region Actions

[DuckyAction]
public record Increment(int Amount = 1);

[DuckyAction]
public record Decrement(int Amount = 1);

[DuckyAction]
public record Reset;

[DuckyAction]
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
        => new(0);

    private static CounterState Reduce(CounterState state, Increment action)
        => new(state.Value + action.Amount);

    private static CounterState Reduce(CounterState state, Decrement action)
        => new(state.Value - action.Amount);

    private static CounterState Reduce(CounterState _, SetValue action)
        => new(action.Value);
}

#endregion

#region Effects

public class ResetCounterAfter3Sec : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        // if the Value is greater than 15, then reset the counter
        if (counterState.Value <= 15)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        Dispatcher?.Reset();
    }
}

#endregion
