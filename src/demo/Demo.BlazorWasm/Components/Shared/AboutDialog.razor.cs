// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class AboutDialog
{
    private string _fullTitle = string.Empty;
    private string _version = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    protected override void OnAfterSubscribed()
    {
        // Get the initial state safely
        LayoutState layoutState = Store.GetSlice<LayoutState>();
        _fullTitle = layoutState.SelectFullTitle();
        _version = layoutState.Version;
    }

    protected override void OnParametersSet()
    {
        // Update when state changes
        LayoutState layoutState = Store.GetSlice<LayoutState>();
        _fullTitle = layoutState.SelectFullTitle();
        _version = layoutState.Version;
    }
}
