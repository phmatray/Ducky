using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Groups and registers reactive effects for use with <see cref="ReactiveEffectMiddleware{TState}"/>.
/// </summary>
/// <typeparam name="TState">Type of the Redux state.</typeparam>
public sealed class ReactiveEffectGroup<TState>
{
    private readonly List<IEffectRegistration<object>> _registrations = [];

    /// <summary>
    /// Register an effect handler for actions.
    /// </summary>
    /// <param name="handler">A function that receives the action stream and state stream, and returns actions to dispatch.</param>
    public void On(Func<Observable<object>, Observable<object>, Observable<object>> handler)
    {
        _registrations.Add(new EffectRegistration<object, object>(handler));
    }

    /// <summary>
    /// Called by the middleware to activate all registered effects.
    /// </summary>
    /// <param name="actions">The observable stream of actions.</param>
    /// <param name="state">The observable stream of state.</param>
    public IEnumerable<Observable<object>> Activate(
        Observable<object> actions,
        Observable<object> state)
        => _registrations.Select(r => r.Connect(actions, state));
}
