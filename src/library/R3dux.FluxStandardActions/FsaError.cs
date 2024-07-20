// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux;

/// <summary>
/// A Flux Standard action with an error property.
/// </summary>
/// <param name="Payload">The error payload of the action.</param>
public abstract record FsaError(Exception Payload)
    : Fsa, IFsaPayload<Exception>
{
    /// <summary>
    /// Gets the error payload of the action.
    /// </summary>
    public Exception Payload { get; init; } = Payload;

    /// <summary>
    /// Gets a value indicating whether the action is an error.
    /// </summary>
    public static bool Error => true;
}
