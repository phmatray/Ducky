// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents an entity with an identifier.
/// </summary>
/// <typeparam name="TKey">The type of the entity's key.</typeparam>
public interface IEntity<out TKey>
    where TKey : notnull
{
    /// <summary>
    /// Gets the identifier of the entity.
    /// </summary>
    TKey Id { get; }
}
