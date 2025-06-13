// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Blazor.Middlewares.Persistence;
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
        On<HydrateSliceAction>(HandleHydration);
    }

    public override CounterState GetInitialState()
        => new(0);

    private static CounterState Reduce(CounterState state, Increment action)
        => new(state.Value + action.Amount);

    private static CounterState Reduce(CounterState state, Decrement action)
        => new(state.Value - action.Amount);

    private static CounterState Reduce(CounterState _, SetValue action)
        => new(action.Value);

    private CounterState HandleHydration(CounterState state, HydrateSliceAction action)
    {
        // Check if this hydration is for our slice
        if (action.SliceKey != GetKey())
            return state;

        // Deserialize the state
        if (action.State is System.Text.Json.JsonElement jsonElement)
        {
            try
            {
                var hydratedState = jsonElement.Deserialize<CounterState>();
                return hydratedState ?? state;
            }
            catch
            {
                // If deserialization fails, return current state
                return state;
            }
        }

        return state;
    }
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
        Dispatcher.Reset();
    }
}

#endregion
