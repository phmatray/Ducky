// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Extensions.FluxStandardActions;

/// <summary>
/// A Flux Standard action with metadata properties.
/// </summary>
/// <param name="Meta">The metadata of the action.</param>
/// <typeparam name="TMeta"></typeparam>
public abstract record FsaMeta<TMeta>(TMeta Meta)
    : Fsa, IFsaMeta<TMeta>
{
    /// <inheritdoc />
    public TMeta Meta { get; init; } = Meta;
}
