using R3;

namespace R3dux;

/// <summary>
/// Represents an effect that handles a stream of actions and interacts with the store's state.
/// </summary>
public interface IEffect
{
    /// <summary>
    /// Handles a stream of actions and produces a stream of resulting actions.
    /// </summary>
    /// <param name="actions">The source observable sequence of actions.</param>
    /// <param name="rootState">The source observable sequence of the root state.</param>
    /// <returns>An observable sequence of resulting actions.</returns>
    Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState);
}
