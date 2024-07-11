namespace Demo.AppStore;

#region State

public record LayoutState
{
    public required string Title { get; init; }
    public required string Version { get; init; }
    public required bool IsModalOpen { get; init; }
    
    // Selectors
    public string SelectFullTitle()
        => $"{Title} - {Version}";
}

#endregion

#region Actions

public record SetTitle(string Title) : IAction;
public record OpenModal : IAction;
public record CloseModal : IAction;

#endregion

#region Reducers

public class LayoutReducers : ReducerCollection<LayoutState>
{
    public LayoutReducers()
    {
        Map<SetTitle>((state, action)
            => state with { Title = action.Title });
        
        Map<OpenModal>((state, _)
            => state with { IsModalOpen = true });
        
        Map<CloseModal>((state, _)
            => state with { IsModalOpen = false });
    }

    public override LayoutState GetInitialState()
    {
        return new LayoutState
        {
            Title = "R3dux",
            Version = "v1.0.0",
            IsModalOpen = false
        };
    }
}

#endregion

#region Slice

// ReSharper disable once UnusedType.Global
public record LayoutSlice : Slice<LayoutState>
{
    public override ReducerCollection<LayoutState> Reducers { get; } = new LayoutReducers();

    public override string GetKey() => "layout";
}

#endregion
