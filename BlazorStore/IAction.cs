namespace BlazorStore;

/// <summary>
/// Marker interface for actions that can be dispatched to a reducer.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Gets the type of the action.
    /// </summary>
    public string Type
        => GetType().Name;
}