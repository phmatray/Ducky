// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class JsonMarkup
{
    private MarkupString _dataColorized;

    [Parameter]
    [EditorRequired]
    public required string Data { get; set; }

    protected override void OnInitialized()
    {
        _dataColorized = (MarkupString)JsonColorizer.ColorizeJson(Data);
    }
}
