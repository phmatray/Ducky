// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when an unhandled exception occurs in an effect.
/// </summary>
public sealed class EffectErrorEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EffectErrorEventArgs"/> class.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="effectType">The type of effect that threw the exception.</param>
    /// <param name="action">The action that triggered the effect.</param>
    /// <param name="isHandled">Whether the exception has been handled and should not propagate further.</param>
    public EffectErrorEventArgs(Exception exception, Type effectType, object action, bool isHandled = false)
    {
        Exception = exception;
        EffectType = effectType;
        Action = action;
        IsHandled = isHandled;
    }

    /// <summary>
    /// Gets the exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the type of effect that threw the exception.
    /// </summary>
    public Type EffectType { get; }

    /// <summary>
    /// Gets the action that triggered the effect.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets a value indicating whether the exception has been handled and should not propagate further.
    /// </summary>
    public bool IsHandled { get; }
}
