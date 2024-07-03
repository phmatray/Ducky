using System.Reflection;

namespace BzRx.MetaReducers;

public static class ImmutabilityReducer
{
    public static ActionReducer<TState, TAction> ImmutabilityCheckMetaReducer<TState, TAction>(
        ActionReducer<TState, TAction> reducer,
        Func<TAction, bool> actionCheck,
        Func<bool> stateCheck)
        where TAction : IAction
    {
        return (state, action) =>
        {
            var act = actionCheck(action) ? Freeze(action) : action;
            var nextState = reducer(state, act);
            return stateCheck() ? Freeze(nextState) : nextState;
        };
    }

    private static object? Freeze(object? target)
    {
        if (target == null || target.GetType().IsPrimitive || target is string)
        {
            return target;
        }

        var targetType = target.GetType();
        if (targetType.IsArray)
        {
            foreach (var item in (Array)target)
            {
                Freeze(item);
            }
        }
        else
        {
            foreach (var prop in targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p is { CanRead: true, CanWrite: true } && !p.Name.StartsWith("ɵ")))
            {
                var propValue = prop.GetValue(target);
                if (propValue != null && !IsImmutable(propValue))
                {
                    Freeze(propValue);
                }
            }
        }

        return target;
    }

    private static bool IsImmutable(object obj)
    {
        var type = obj.GetType();
        return type.IsPrimitive || obj is string || obj is DateTime || obj is TimeSpan;
    }
}