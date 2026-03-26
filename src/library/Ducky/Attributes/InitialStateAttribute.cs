// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Marks a static field or property as the initial state for a [DuckyReducer] class.
/// If not specified, the generator uses <c>new TState()</c> as default.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InitialStateAttribute : Attribute;
