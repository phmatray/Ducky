// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Extensions.FluxStandardActions;

/// <summary>
/// A Flux Standard action with an error property and metadata properties.
/// </summary>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public abstract record FsaError<TMeta>(Exception Payload, TMeta Meta)
    : FsaError(Payload), IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}
