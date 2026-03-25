// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class SampleForm
{
    private MudForm _login = null!;

    [Parameter]
    [EditorRequired]
    public EventCallback OnLogin { get; set; }
}
