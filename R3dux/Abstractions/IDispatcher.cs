using R3;
using R3dux.Temp;

namespace R3dux;

/// <summary>
/// Defines the contract for a dispatcher that can dispatch actions and provide an observable stream of dispatched actions.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Gets an observable stream of dispatched actions.
    /// </summary>
    Observable<IAction> ActionStream { get; }
    
    /// <summary>
    /// Dispatches the specified action.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> is null.</exception>
    void Dispatch(IAction action);
}