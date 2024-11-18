// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Abstractions;

namespace Ducky.Extensions.FluxStandardActions;

/// <summary>
/// A Flux Standard action without payload or metadata properties.
/// </summary>
public abstract record Fsa
    : IKeyedAction
{
    /// <inheritdoc />
    public abstract string TypeKey { get; }
}
