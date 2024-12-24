// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents a pair of state and action.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
/// <typeparam name="TAction">The type of the action.</typeparam>
/// <param name="State">The current state.</param>
/// <param name="Action">The action to be performed.</param>
public sealed record StateActionPair<TState, TAction>(TState State, TAction Action);
