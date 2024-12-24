// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// A Flux Standard action with a generic payload type.
/// </summary>
/// <param name="Payload">The payload of the action.</param>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
public abstract record Fsa<TPayload>(TPayload Payload)
    : Fsa, IFsaPayload<TPayload>
{
    /// <inheritdoc />
    public TPayload Payload { get; init; } = Payload;
}
