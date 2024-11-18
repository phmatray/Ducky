// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents a state change notification.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <param name="Action">The action causing the state change.</param>
/// <param name="PreviousState">The previous state before the change.</param>
/// <param name="NewState">The new state after the change.</param>
/// <param name="ElapsedMilliseconds">The time taken for the state change in milliseconds.</param>
public sealed record StateChange<TState>(
    IAction Action,
    TState PreviousState,
    TState NewState,
    double ElapsedMilliseconds);
