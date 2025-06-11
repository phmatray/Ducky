// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;

#pragma warning disable SA1402, SA1649

namespace Ducky.Tests.TestModels;

// Actions
public sealed record TestIncrementAction;

public sealed record TestDecrementAction;

public sealed record TestResetAction;

public sealed record TestSetValueAction(int Value);

// Reducers
public sealed record TestCounterReducers : SliceReducers<int>
{
    public TestCounterReducers()
    {
        On<TestIncrementAction>((state, _) => state + 1);
        On<TestDecrementAction>((state, _) => state - 1);
        On<TestResetAction>((_, _) => GetInitialState());
        On<TestSetValueAction>((_, action) => action.Value);
    }

    public override int GetInitialState()
    {
        return 10;
    }
}

// Effects
public sealed class TestIncrementEffect : AsyncEffect<TestIncrementAction>
{
    public override async Task HandleAsync(TestIncrementAction action, IRootState rootState)
    {
        // if the Value is greater than 15, then reset the counter
        int state = rootState.GetSliceState<int>();
        if (state <= 15)
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(3));
        Dispatcher?.Dispatch(new TestResetAction());
    }
}

#pragma warning restore SA1402, SA1649
