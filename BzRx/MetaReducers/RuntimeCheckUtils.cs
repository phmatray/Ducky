using System.Collections;

namespace BzRx.MetaReducers;

public static class RuntimeCheckUtils
{
    public static bool IsUndefined(this object? target)
        => target == null;

    public static bool IsNull(this object? target)
        => target == null;

    public static bool IsArray(this object? target)
        => target is IEnumerable and not string;

    public static bool IsString(this object? target)
        => target is string;

    public static bool IsBoolean(this object? target)
        => target is bool;

    public static bool IsNumber(this object? target)
        => target is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    public static bool IsObjectLike(this object? target)
        => target != null && !IsPrimitive(target);

    public static bool IsObject(this object? target)
        => IsObjectLike(target) && !IsArray(target);

    public static bool IsPlainObject(this object? target)
    {
        if (!IsObject(target))
        {
            return false;
        }

        var targetType = target?.GetType();
        return targetType?.BaseType == typeof(object) || targetType?.BaseType == null;
    }

    public static bool IsFunction(this object? target)
        => target is Delegate;

    public static bool IsComponent(this object? target)
        => IsFunction(target) && target?.GetType().GetProperty("ɵcmp") != null;

    public static bool HasOwnProperty(this object? target, string propertyName)
        => target?.GetType().GetProperty(propertyName) != null;

    public static bool IsPrimitive(this object? obj)
        => obj?.GetType().IsPrimitive == true || obj is string or DateTime or TimeSpan;
}