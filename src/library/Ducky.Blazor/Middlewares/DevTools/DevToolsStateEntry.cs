// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Represents a single entry in the DevTools action history for time-travel debugging.
/// </summary>
internal record DevToolsStateEntry(
    int SequenceNumber,
    object Action,
    string SerializedState,
    bool IsSkipped,
    DateTime Timestamp)
{
    public DevToolsStateEntry WithToggledSkip()
        => this with { IsSkipped = !IsSkipped };
}
