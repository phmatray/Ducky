using R3;

namespace Ducky.Reactive;

/// <summary>Registration and operators for reactive effects.</summary>
public static class DuckyReactiveExtensions
{
    /// <summary>Filters the action stream to actions whose runtime type is exactly <typeparamref name="TAction"/>.</summary>
    /// <typeparam name="TAction">The action type; derived types are not matched.</typeparam>
    /// <param name="actions">The action stream.</param>
    /// <returns>The actions of exactly that type.</returns>
    public static Observable<TAction> OfActionType<TAction>(this Observable<object> actions)
        where TAction : notnull =>
        actions.Where(static action => action.GetType() == typeof(TAction)).Select(static action => (TAction)action);
}
