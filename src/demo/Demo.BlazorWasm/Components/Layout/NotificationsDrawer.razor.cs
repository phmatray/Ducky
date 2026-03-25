// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Layout;

public partial class NotificationsDrawer
{
    [Parameter]
    [EditorRequired]
    public bool IsOpen { get; set; }

    private ValueCollection<Notification> UnreadNotifications
        => State.SelectUnreadNotifications();

    private bool HasUnreadNotifications
        => State.SelectHasUnreadNotifications();

    private void MarkNotificationAsRead(Guid id)
    {
        Dispatcher.MarkNotificationAsRead(id);
    }

    private void MarkAllNotificationsAsRead()
    {
        Dispatcher.MarkAllNotificationsAsRead();
    }

    private void GoToErrorDetails(Guid id)
    {
        NavigationManager.NavigateTo($"/errors#{id}");
    }
}
