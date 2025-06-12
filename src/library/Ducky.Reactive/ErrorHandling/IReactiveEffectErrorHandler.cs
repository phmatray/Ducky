// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive;

/// <summary>
/// Interface for handling errors that occur in reactive effects.
/// </summary>
public interface IReactiveEffectErrorHandler
{
    /// <summary>
    /// Handles an error that occurred in a reactive effect.
    /// </summary>
    /// <param name="error">The exception that occurred.</param>
    /// <param name="effectType">The type of effect that failed.</param>
    /// <param name="action">The action being processed when the error occurred.</param>
    /// <returns>A result indicating how to proceed.</returns>
    Task<ErrorHandlingResult> HandleErrorAsync(
        Exception error,
        Type effectType,
        object? action);

    /// <summary>
    /// Called when an effect is being retried after an error.
    /// </summary>
    /// <param name="effectType">The type of effect being retried.</param>
    /// <param name="retryCount">The current retry attempt number.</param>
    /// <param name="lastError">The last error that occurred.</param>
    void OnRetry(Type effectType, int retryCount, Exception lastError);
}

/// <summary>
/// Result of error handling indicating how to proceed.
/// </summary>
public class ErrorHandlingResult
{
    /// <summary>
    /// Gets a value indicating whether to continue processing.
    /// </summary>
    public bool ShouldContinue { get; init; }

    /// <summary>
    /// Gets a value indicating whether to retry the operation.
    /// </summary>
    public bool ShouldRetry { get; init; }

    /// <summary>
    /// Gets the delay before retrying (if ShouldRetry is true).
    /// </summary>
    public TimeSpan? RetryDelay { get; init; }

    /// <summary>
    /// Gets an optional replacement action to dispatch.
    /// </summary>
    public object? ReplacementAction { get; init; }

    /// <summary>
    /// Creates a result that continues processing without retry.
    /// </summary>
    public static ErrorHandlingResult Continue() => new() { ShouldContinue = true };

    /// <summary>
    /// Creates a result that stops processing.
    /// </summary>
    public static ErrorHandlingResult Stop() => new() { ShouldContinue = false };

    /// <summary>
    /// Creates a result that retries the operation.
    /// </summary>
    /// <param name="delay">Optional delay before retry.</param>
    public static ErrorHandlingResult Retry(TimeSpan? delay = null) => new()
    {
        ShouldContinue = true,
        ShouldRetry = true,
        RetryDelay = delay
    };

    /// <summary>
    /// Creates a result that replaces the failed action with another.
    /// </summary>
    /// <param name="action">The replacement action.</param>
    public static ErrorHandlingResult Replace(object action) => new()
    {
        ShouldContinue = true,
        ReplacementAction = action
    };
}
