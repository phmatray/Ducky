// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Extensions.FluxStandardActions;

/// <summary>
/// Represents an action with a payload property.
/// </summary>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public interface IFsaPayload<TPayload>
{
    /// <summary>
    /// Gets the optional `payload` property MAY be any type of value.
    /// </summary>
    TPayload Payload { get; init; }
}
