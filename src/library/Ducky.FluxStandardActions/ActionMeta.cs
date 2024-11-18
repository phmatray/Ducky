// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Extensions.FluxStandardActions;

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
        return new ActionMeta(DateTime.UtcNow);
    }
}
