// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Normalization;

/// <summary>
/// Defines strategies for merging entities into the state.
/// </summary>
public enum MergeStrategy
{
    /// <summary>
    /// Fail if a duplicate entity is found during the merge.
    /// </summary>
    FailIfDuplicate,

    /// <summary>
    /// Overwrite existing entities with the same key during the merge.
    /// </summary>
    Overwrite,
}
