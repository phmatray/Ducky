// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class ExceptionDetailsAlert
{
    private const string Separator = "•";

    [Parameter]
    [EditorRequired]
    public required Notification Notification { get; set; }

    [Parameter]
    public string SearchTerm { get; set; } = string.Empty;

    private void MarkNotificationAsRead(Guid id)
        => Dispatcher.MarkNotificationAsRead(id);
}
