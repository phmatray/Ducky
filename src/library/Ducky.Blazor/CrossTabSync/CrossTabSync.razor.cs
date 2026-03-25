// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Blazor.CrossTabSync;

/// <summary>
/// A Blazor component that synchronizes state across browser tabs.
/// </summary>
public partial class CrossTabSync
{
    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        await TabSync.StartAsync().ConfigureAwait(false);
    }
}
