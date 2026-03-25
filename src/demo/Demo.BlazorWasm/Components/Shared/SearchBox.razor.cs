// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class SearchBox
{
    [Parameter]
    public string Label { get; set; } = "Search";

    [Parameter]
    public string Class { get; set; } = string.Empty;

    [Parameter]
    public int DebounceInterval { get; set; } = 300;

    [Parameter]
    public EventCallback<string> OnSearch { get; set; }

    private string SearchTerm { get; set; } = string.Empty;

    private async Task OnDebounceIntervalElapsedAsync(string value)
    {
        await OnSearch.InvokeAsync(value);
    }
}
