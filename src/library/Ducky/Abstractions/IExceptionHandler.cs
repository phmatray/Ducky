// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky;

/// <summary>
/// Interface for handling global exceptions in the Ducky store.
/// </summary>
public interface IExceptionHandler
{
    /// <summary>
    /// Handles an exception that occurred during action processing.
    /// </summary>
    /// <param name="eventArgs">The event arguments containing exception details.</param>
    /// <returns>True if the exception was handled and should not propagate further, false otherwise.</returns>
    bool HandleActionError(ActionErrorEventArgs eventArgs);

    /// <summary>
    /// Handles an exception that occurred in an effect.
    /// </summary>
    /// <param name="eventArgs">The event arguments containing exception details.</param>
    /// <returns>True if the exception was handled and should not propagate further, false otherwise.</returns>
    bool HandleEffectError(EffectErrorEventArgs eventArgs);
}
