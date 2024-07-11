using System.Text.Json;
using System.Text.Json.Serialization;
using R3;

namespace R3dux;

/// <summary>
/// Provides logging functionalities for state changes.
/// </summary>
public sealed class StateLoggerObserver<TState>
    : Observer<StateChange<TState>>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Handles the reception of a state change notification.
    /// </summary>
    /// <param name="stateChange">The state change notification.</param>
    protected override void OnNextCore(StateChange<TState> stateChange)
    {
        LogStateChange(stateChange.Action, stateChange.PreviousState, stateChange.NewState,
            stateChange.ElapsedMilliseconds);
    }

    /// <summary>
    /// Handles the completion of the state change stream.
    /// </summary>
    protected override void OnCompletedCore(Result result)
    {
        Console.WriteLine("State change observation completed.");
    }

    /// <summary>
    /// Handles an error in the state change stream.
    /// </summary>
    /// <param name="error">The error encountered.</param>
    protected override void OnErrorResumeCore(Exception error)
    {
        Console.WriteLine($"State change observation error: {error.Message}");
    }

    /// <summary>
    /// Logs the details of a state change.
    /// </summary>
    /// <param name="action">The action causing the state change.</param>
    /// <param name="prevState">The previous state before the change.</param>
    /// <param name="newState">The new state after the change.</param>
    /// <param name="elapsedMilliseconds">The time taken for the state change in milliseconds.</param>
    private void LogStateChange(IAction action, TState prevState, TState newState, double elapsedMilliseconds)
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

        // if obj is null, return "null"
        if (obj is null)
        {
            return nullString;
        }

        try
        {
            return JsonSerializer
                .Serialize(obj, SerializerOptions)
                .Replace(Environment.NewLine, $"{Environment.NewLine}  ");
        }
        catch (Exception ex)
        {
            return $"Error serializing object: {ex.Message}";
        }
    }
}