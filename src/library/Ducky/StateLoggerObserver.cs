// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using R3;

namespace Ducky;

/// <summary>
/// Provides logging functionalities for state changes.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public sealed class StateLoggerObserver<TState> : Observer<StateChange<TState>>
{
    private static readonly ILogger<StateLoggerObserver<TState>> Logger =
        LoggerProvider.CreateLogger<StateLoggerObserver<TState>>()
            ?? throw new InvalidOperationException("Logger not found.");

    // ReSharper disable once StaticMemberInGenericType
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
    };

    /// <summary>
    /// Handles the reception of a state change notification.
    /// </summary>
    /// <param name="value">The state change notification.</param>
    protected override void OnNextCore(StateChange<TState> value)
    {
        LogStateChange(value);
    }

    /// <summary>
    /// Handles the completion of the state change stream.
    /// </summary>
    /// <param name="result">The result of the state change stream.</param>
    protected override void OnCompletedCore(Result result)
    {
        Logger.StateChangeObservationCompleted();
    }

    /// <summary>
    /// Handles an error in the state change stream.
    /// </summary>
    /// <param name="error">The error encountered.</param>
    protected override void OnErrorResumeCore(Exception error)
    {
        Logger.StateChangeObservationError();
    }

    /// <summary>
    /// Logs the state change.
    /// </summary>
    /// <param name="action">The action that caused the state change.</param>
    private static void LogStateChange(StateChange<TState> action)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        string actionName = GetActionType(action.Action);
        string sliceType = action.PreviousState?.GetType().ToString() ?? "Unknown";
        string duration = string.Format(CultureInfo.InvariantCulture, "{0:F2}", action.ElapsedMilliseconds);
        string prevStateDetails = GetObjectDetails(action.PreviousState);
        string actionDetails = GetObjectDetails(action.Action);
        string nextStateDetails = GetObjectDetails(action.NewState);

        Logger.LogStateChange(
            actionName, timestamp, duration, sliceType, prevStateDetails, actionDetails, nextStateDetails);
    }

    /// <summary>
    /// Gets the name of the action type.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The name of the action type.</returns>
    private static string GetActionType(IAction action)
    {
        // if the action object has a property named "TypeKey", return its value
        return (action.GetType().GetProperty(nameof(IKeyedAction.TypeKey)) is { } typeKeyProperty)
            ? typeKeyProperty.GetValue(action)?.ToString() ?? GetActionName()
            : GetActionName();

        string GetActionName() => action.GetType().Name;
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
