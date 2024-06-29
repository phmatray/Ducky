namespace Demo.AppStore;

// State
public record LayoutState
{
    public string Title { get; init; } = "R3dux";
    public string Version { get; init; } = "v1.0.0";
    public bool IsModalOpen { get; init; } = false;
    
    // Selectors
    public string SelectFullTitle()
        => $"{Title} - {Version}";
}

// Actions
public record SetTitle(string Title) : IAction;
public record OpenModal : IAction;
public record CloseModal : IAction;


// Reducer
public class LayoutReducer : Reducer<LayoutState>
{
    public override LayoutState Reduce(LayoutState state, IAction action)
        => action switch
        {
            SetTitle setTitleAction => state with { Title = setTitleAction.Title },
            OpenModal => state with { IsModalOpen = true },
            CloseModal => state with { IsModalOpen = false },
            _ => state
        };
}