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

public record LayoutSlice : Slice<LayoutState>
{
    
}

// Actions
public record SetTitle(string Title);
public record OpenModal;
public record CloseModal;


// Reducers
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
}