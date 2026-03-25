// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageMovieDetails
{
    [Parameter]
    public required int Id { get; set; }

    private Movie? SelectedMovie
        => State.SelectMovieById(Id);
}
