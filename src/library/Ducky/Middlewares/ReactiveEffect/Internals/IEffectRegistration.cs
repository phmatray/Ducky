using R3;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Interface for effect registration.
/// </summary>
/// <typeparam name="TState">The type of the Redux state.</typeparam>
internal interface IEffectRegistration<TState>
{
    Observable<object> Connect(Observable<object> actions, Observable<TState> state);
}
