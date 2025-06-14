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
