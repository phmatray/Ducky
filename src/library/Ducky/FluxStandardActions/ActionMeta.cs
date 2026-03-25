// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents the metadata properties of an action.
/// </summary>
/// <param name="TimeStamp">The time the action was created.</param>
public sealed record ActionMeta(DateTime TimeStamp)
{
    /// <summary>
    /// Creates a new instance of the <see cref="ActionMeta"/> record.
    /// </summary>
    /// <returns>A new instance of the <see cref="ActionMeta"/> record.</returns>
    public static ActionMeta Create()
    {
        return new(DateTime.UtcNow);
    }
}
