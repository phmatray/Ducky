using System.Collections;

namespace BlazorStore;

public static class StateLogger
{
    public static void LogStateChange<TState>(
        IAction action, TState prevState, TState newState, double elapsedMilliseconds)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var actionName = GetActionType(action);
        Console.WriteLine();
        Console.WriteLine($"action {actionName} @ {timestamp} (in {elapsedMilliseconds:F2} ms)");
        Console.WriteLine($"  prev state → {GetObjectDetails(prevState)}");
        Console.WriteLine($"  action     → {actionName} {GetObjectDetails(action)}");
        Console.WriteLine($"  next state → {GetObjectDetails(newState)}");
    }

    private static string GetActionType(IAction action)
    {
        return action.GetType().Name;
    }

    private static string GetObjectDetails(object? obj)
    {
        if (obj is null)
        {
            return "null";
        }
        
        var properties = obj.GetType().GetProperties()
            .Select(p =>
            {
                var value = p.GetValue(obj);
                return value is IEnumerable collection and not string
                    ? $"{p.Name}: {collection.Cast<object>().Count()} items"
                    : $"{p.Name}: {value ?? "null"}";
            })
            .ToArray();
        
        return properties.Length == 0
            ? "{{ }}"
            : $"{{ {string.Join(", ", properties)} }}";
    }  
}