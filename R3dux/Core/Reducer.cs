using R3dux.Temp;

namespace R3dux;

/// <summary>
/// Represents a reducer that processes actions and returns a new state.
/// </summary>
/// <param name="reduceFunction">The function that reduces the state.</param>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TAction">The type of the action.</typeparam>
public class Reducer<TState, TAction>(
    Func<TState, TAction, TState> reduceFunction)
    : IReducer<TState, TAction>
    where TAction : IAction
{
    private readonly Func<TState, TAction, TState> _reduceFunction =
        reduceFunction
        ?? throw new ArgumentNullException(nameof(reduceFunction));

    /// <inheritdoc />
    public TState Reduce(TState state, TAction action)
    {
        return _reduceFunction(state, action);
    }
}