namespace BzRx.MetaReducers;

public class SerializationReducer
{
    public static ActionReducer<TState, TAction> SerializationCheckMetaReducer<TState, TAction>(
        ActionReducer<TState, TAction> reducer,
        Func<TAction, bool> actionCheck,
        Func<bool> stateCheck)
        where TAction : IAction
    {
        return (state, action) =>
        {
            if (actionCheck(action))
            {
                var unserializableAction = GetUnserializable(action);
                ThrowIfUnserializable(unserializableAction, "action");
            }

            var nextState = reducer(state, action);

            if (stateCheck())
            {
                var unserializableState = GetUnserializable(nextState);
                ThrowIfUnserializable(unserializableState, "state");
            }

            return nextState;
        };
    }

    private static (string[] Path, object Value)? GetUnserializable(object target, List<string>? path = null)
    {
        path ??= new List<string>();

        if ((target.IsUndefined() || target.IsNull()) && path.Count == 0)
        {
            return (new[] { "root" }, target);
        }

        var targetType = target.GetType();
        var keys = targetType.GetProperties().Select(p => p.Name);

        foreach (var key in keys)
        {
            var value = targetType.GetProperty(key).GetValue(target);

            if (value.IsComponent())
            {
                continue;
            }

            if (value.IsUndefined() ||
                value.IsNull() ||
                value.IsNumber() ||
                value.IsBoolean() ||
                value.IsString() ||
                value.IsArray())
            {
                continue;
            }

            if (value.IsPlainObject())
            {
                var unserializable = GetUnserializable(value, path.Append(key).ToList());
                if (unserializable.HasValue)
                {
                    return unserializable;
                }
            }
            else
            {
                return (path.Append(key).ToArray(), value);
            }
        }

        return null;
    }

    private static void ThrowIfUnserializable((string[] Path, object Value)? unserializable, string context)
    {
        if (unserializable == null)
        {
            return;
        }

        var (path, value) = unserializable.Value;
        var unserializablePath = string.Join(".", path);
        var error = new Exception($"Detected unserializable {context} at \"{unserializablePath}\".")
        {
            Data =
            {
                ["Value"] = value,
                ["UnserializablePath"] = unserializablePath
            }
        };
        throw error;
    }
}