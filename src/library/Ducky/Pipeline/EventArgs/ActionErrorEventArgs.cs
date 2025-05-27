// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when an unhandled exception occurs during action processing.
/// </summary>
public sealed class ActionErrorEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionErrorEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="action">The action that was being processed when the exception occurred.</param>
    /// <param name="context">The action context at the time of the exception.</param>
    /// <param name="isHandled">Whether the exception has been handled and should not propagate further.</param>
    public ActionErrorEventArgs(Exception exception, object action, ActionContext context, bool isHandled = false)
    {
        Exception = exception;
        Action = action;
        Context = context;
        IsHandled = isHandled;
    }

    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the action that was being processed when the exception occurred.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the action context at the time of the exception.
    /// </summary>
    public ActionContext Context { get; }

    /// <summary>
    /// Gets a value indicating whether the exception has been handled and should not propagate further.
    /// </summary>
    public bool IsHandled { get; }
}
