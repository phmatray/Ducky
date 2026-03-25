// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Pipeline;

/// <summary>
/// Event arguments for when the store begins disposal.
/// </summary>
public sealed class StoreDisposingEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreDisposingEventArgs"/> class.
    /// </summary>
    /// <param name="uptime">The uptime of the store before disposal.</param>
    public StoreDisposingEventArgs(in TimeSpan uptime)
    {
        Uptime = uptime;
    }

    /// <summary>
    /// Gets the uptime of the store before disposal.
    /// </summary>
    public TimeSpan Uptime { get; }
}
