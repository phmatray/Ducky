using R3;

namespace Ducky.Middlewares.ReactiveEffect;

internal class EffectRegistration<TAction, TState>(
    Func<Observable<TAction>, Observable<TState>, Observable<object>> handler)
    : IEffectRegistration<TState>
{
    public Observable<object> Connect(Observable<object> actions, Observable<TState> state)
        => handler(actions.OfType<object, TAction>(), state);
}
