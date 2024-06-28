namespace Demo.AppStore;

// State
public record LayoutState
{
    public string Title { get; init; } = "Demo App Store";
    public string Version { get; init; } = "v1.0.0";
    
    public string FullTitle
        => $"{Title} - {Version}";
}

// Actions
public record SetTitle(string Title) : IAction;


// Reducer
public class LayoutReducer : Reducer<LayoutState>
{
    public override LayoutState Reduce(LayoutState state, IAction action)
        => action switch
        {
            SetTitle setTitleAction => state with { Title = setTitleAction.Title },
            _ => state
        };
}