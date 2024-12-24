// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// A Flux Standard action with a generic payload and metadata type.
/// </summary>
/// <param name="Payload">The payload of the action.</param>
/// <param name="Meta">The metadata of the action.</param>
/// <typeparam name="TPayload">The type of the payload.</typeparam>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public abstract record Fsa<TPayload, TMeta>(TPayload Payload, TMeta Meta)
    : Fsa<TPayload>(Payload), IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}
