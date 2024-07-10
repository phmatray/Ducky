namespace R3dux;

/// <summary>
/// Represents a state change notification.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <param name="Action">The action causing the state change.</param>
/// <param name="PreviousState">The previous state before the change.</param>
/// <param name="NewState">The new state after the change.</param>
/// <param name="ElapsedMilliseconds">The time taken for the state change in milliseconds.</param>
public record StateChange<TState>(
    IAction Action,
    TState PreviousState,
    TState NewState,
    double ElapsedMilliseconds);