using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Ducky;

// SPEC §9 (DT-02): an action's type name, computed once per action type per store. [ActionType] is the one attribute
// read at run time (§5.8): trim-safe, and it works for actions from assemblies that never ran the generator.
internal sealed class ActionTypes
{
    private readonly ConcurrentDictionary<Type, string> _names = new();

    public string Of(object action) => _names.GetOrAdd(action.GetType(), Compute);

    // SetState<T> => {sliceKey}/{name} joins with SetState itself (M9-01, stage 8).
    private static string Compute(Type type) =>
        type.GetCustomAttribute<ActionTypeAttribute>(inherit: false)?.Name
        ?? (type == typeof(HydrateSlices) ? "@ducky/restore" : Format(type));

    // Namespace.Outer.Inner, generic arguments as Name<Arg> named by the same rule, each nesting level with its own
    // arguments (Outer<A>.Inner<B>: the `N suffix of a level's Name counts the parameters that level declares), arrays as
    // Element[] / Element[,].
    private static string Format(Type type)
    {
        if (type.IsArray)
        {
            return $"{Format(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
        }

        var levels = new Stack<Type>();
        for (var level = type; level is not null; level = level.DeclaringType)
        {
            levels.Push(level);
        }

        var args = type.GenericTypeArguments;
        var used = 0;
        var parts = new List<string>(levels.Count);
        foreach (var level in levels)
        {
            var tick = level.Name.IndexOf('`', StringComparison.Ordinal);
            if (tick < 0)
            {
                parts.Add(level.Name);
                continue;
            }

            var arity = int.Parse(level.Name.AsSpan(tick + 1), CultureInfo.InvariantCulture);
            parts.Add($"{level.Name[..tick]}<{string.Join(", ", args.Skip(used).Take(arity).Select(Format))}>");
            used += arity;
        }

        var name = string.Join('.', parts);
        return type.Namespace is { } ns ? $"{ns}.{name}" : name;
    }
}
