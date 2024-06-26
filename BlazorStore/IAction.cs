namespace BlazorStore;

/// <summary>
/// Marker interface for actions that can be dispatched to a reducer.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Gets the type of the action.
    /// </summary>
    string Type
        => GetType().Name;
}