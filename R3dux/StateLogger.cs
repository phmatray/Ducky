using System.Collections;

namespace R3dux;

/// <summary>
/// Provides logging functionalities for state changes.
/// </summary>
public static class StateLogger
{
    /// <summary>
    /// Logs the details of a state change.
    /// </summary>
    /// <typeparam name="TState">The type of the state.</typeparam>
    /// <param name="action">The action causing the state change.</param>
    /// <param name="prevState">The previous state before the change.</param>
    /// <param name="newState">The new state after the change.</param>
    /// <param name="elapsedMilliseconds">The time taken for the state change in milliseconds.</param>
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

    /// <summary>
    /// Gets the name of the action type.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The name of the action type.</returns>
    private static string GetActionType(IAction action)
    {
        return action.GetType().Name;
    }

    /// <summary>
    /// Gets the details of an object.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <returns>A string containing the details of the object.</returns>
    private static string GetObjectDetails(object? obj)
    {
        const string nullString = "NULL";
        const string emptyString = "EMPTY OBJECT";
        
        // if obj is null, return "null"
        if (obj is null)
        {
            return nullString;
        }
        
        // if obj is a basic type, return its string representation
        if (obj.GetType().IsPrimitive || obj is string)
        {
            return obj.ToString() ?? nullString;
        }
        
        var properties = obj.GetType().GetProperties()
            .Select(p =>
            {
                var value = p.GetValue(obj);
                return value is IEnumerable collection and not string
                    ? $"{p.Name}: {collection.Cast<object>().Count()} items"
                    : $"{p.Name}: {value ?? nullString}";
            })
            .ToArray();
        
        return properties.Length == 0
            ? emptyString
            : $"{{ {string.Join(", ", properties)} }}";
    }  
}