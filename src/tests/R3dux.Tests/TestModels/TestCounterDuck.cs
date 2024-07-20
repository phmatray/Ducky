// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

#pragma warning disable SA1402
#pragma warning disable SA1649

namespace R3dux.Tests.TestModels;

// Actions
public sealed record TestIncrementAction : IAction;

public sealed record TestDecrementAction : IAction;

public sealed record TestResetAction : IAction;

public sealed record TestSetValueAction(int Value) : IAction;

// Reducers
public sealed record TestCounterReducers : SliceReducers<int>
{
    public TestCounterReducers()
    {
        Map<TestIncrementAction>((state, _) => state + 1);
        Map<TestDecrementAction>((state, _) => state - 1);
        Map<TestResetAction>((_, _) => GetInitialState());
        Map<TestSetValueAction>((_, action) => action.Value);
    }

    public override int GetInitialState()
    {
        return 10;
    }
}

// Effects
public sealed class TestIncrementEffect : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState)
    {
        // if the Value is greater than 15, then reset the counter
        return actions
            .FilterActions<TestIncrementAction>()
            .WithSliceState<int, TestIncrementAction>(rootState)
            .Where(pair => pair.State > 15)
            .Delay(TimeSpan.FromSeconds(3))
            .SelectAction(_ => new TestResetAction());
    }
}

#pragma warning restore SA1402
#pragma warning restore SA1649
