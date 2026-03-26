// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Marks a static partial class as a reducer definition.
/// The source generator will produce a SliceReducers&lt;TState&gt; subclass
/// from the static On() methods in the class.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DuckyReducerAttribute : Attribute;
