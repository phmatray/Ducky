// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents an action with a metadata property.
/// </summary>
/// <typeparam name="TMeta">The type of the metadata.</typeparam>
public interface IFsaMeta<TMeta>
{
    /// <summary>
    /// Gets the optional `meta` property MAY be any type of value.
    /// </summary>
    TMeta Meta { get; init; }
}
