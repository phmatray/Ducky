// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when the store completes initialization.
/// </summary>
public sealed class StoreInitializedEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreInitializedEventArgs"/> class.
    /// </summary>
    /// <param name="sliceCount">The number of slices registered.</param>
    /// <param name="sliceKeys">The keys of all registered slices.</param>
    public StoreInitializedEventArgs(int sliceCount, IReadOnlyList<string> sliceKeys)
    {
        SliceCount = sliceCount;
        SliceKeys = sliceKeys;
    }

    /// <summary>
    /// Gets the number of slices registered in the store.
    /// </summary>
    public int SliceCount { get; }

    /// <summary>
    /// Gets the keys of all registered slices.
    /// </summary>
    public IReadOnlyList<string> SliceKeys { get; }
}
