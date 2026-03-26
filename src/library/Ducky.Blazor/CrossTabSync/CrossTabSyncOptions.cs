// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// Configuration options for cross-tab state synchronization.
/// </summary>
public class CrossTabSyncOptions
{
    /// <summary>
    /// Whether cross-tab sync is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Debounce delay in milliseconds to prevent rapid-fire hydration. Default: 100.
    /// </summary>
    public int DebounceMs { get; set; } = 100;

    /// <summary>
    /// If specified, only these slice keys are synchronized across tabs.
    /// Null means all slices are synchronized.
    /// </summary>
    public string[]? IncludedSliceKeys { get; set; }

    /// <summary>
    /// The localStorage key used for cross-tab sync.
    /// Defaults to "ducky:state" to match the persistence storage key.
    /// </summary>
    public string StorageKey { get; set; } = "ducky:state";
}
