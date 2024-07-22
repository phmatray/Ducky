// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Goals;

#region State

public record GoalState
{
    public required ImmutableSortedDictionary<string, bool> Goals { get; init; }

    // Selectors
    public bool SelectIsGoalMet(string goalKey)
    {
        return Goals.TryGetValue(goalKey, out var isMet) && isMet;
    }
}

#endregion

#region Actions

public record SetGoalMet(string GoalKey) : IAction;

#endregion

#region Reducers

public record GoalsReducers : SliceReducers<GoalState>
{
    public GoalsReducers()
    {
        On<SetGoalMet>(ReduceSetGoalMet);
    }

    public override GoalState GetInitialState()
    {
        return new GoalState
        {
            Goals = ImmutableSortedDictionary<string, bool>.Empty
        };
    }

    private static GoalState ReduceSetGoalMet(GoalState state, SetGoalMet action)
    {
        return new GoalState
        {
            Goals = state.Goals.SetItem(action.GoalKey, true)
        };
    }
}

#endregion
