// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Abstractions;

/// <summary>
/// Defines a contract for an action with a `type` key.
/// </summary>
public interface IKeyedAction : IAction
{
    /// <summary>
    /// Gets the `type` of an action identifies to the consumer the nature of the action that has occurred.
    /// </summary>
    string TypeKey { get; }
}
