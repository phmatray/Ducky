// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Marks a static partial class as an effect definition.
/// The source generator will produce one AsyncEffect&lt;TAction&gt; class per static method.
/// First parameter is the trigger action type.
/// IStateProvider, IDispatcher, CancellationToken are framework-provided.
/// All other parameters are resolved from DI.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DuckyEffectAttribute : Attribute;
