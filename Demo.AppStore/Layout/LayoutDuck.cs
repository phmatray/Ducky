namespace Demo.AppStore;

#region State

public record LayoutState
{
    public required string Title { get; init; }
    public required string Version { get; init; }
    
    // Selectors
    public string SelectFullTitle()
        => $"{Title} - {Version}";
}

#endregion

#region Actions

public record SetTitle(string Title) : IAction;

#endregion

#region Reducers

public record LayoutReducers : SliceReducers<LayoutState>
{
    public LayoutReducers()
    {
        Map<SetTitle>((state, action)
            => state with { Title = action.Title });
    }

    public override LayoutState GetInitialState()
    {
        return new LayoutState
        {
            Title = "R3dux",
            Version = "v1.0.0"
        };
    }
}

#endregion
