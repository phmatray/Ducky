namespace AppStore;

#region State

public record GoalState
{
    public required ImmutableSortedDictionary<string, bool> Goals { get; init; }

    // Selectors
    public bool SelectIsGoalMet(string goalKey)
        => Goals.TryGetValue(goalKey, out var isMet) && isMet;
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
        Map<SetGoalMet>(ReduceSetGoalMet);
    }

    private static GoalState ReduceSetGoalMet(GoalState state, SetGoalMet action)
        => new() { Goals = state.Goals.SetItem(action.GoalKey, true) };

    public override GoalState GetInitialState()
    {
        return new GoalState
        {
            Goals = ImmutableSortedDictionary<string, bool>.Empty
        };
    }
}

#endregion