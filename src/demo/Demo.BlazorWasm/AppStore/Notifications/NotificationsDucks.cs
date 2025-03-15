// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

#region State

public record NotificationsState
{
    public required ValueCollection<Notification> Notifications { get; init; }

    // Selectors
    public ValueCollection<Notification> SelectUnreadNotifications()
        =>
        [
            ..Notifications
                .Where(n => !n.IsRead)
                .Reverse()
        ];

    public bool SelectHasUnreadNotifications()
        => !SelectUnreadNotifications().IsEmpty;

    public int SelectUnreadNotificationCount()
        => SelectUnreadNotifications().Count;

    public ValueCollection<Notification> SelectNotificationsBySeverity(NotificationSeverity severity)
        => SelectUnreadNotifications()
            .Where(n => n.Severity == severity)
            .ToValueCollection();

    public ValueCollection<Notification> SelectErrorNotifications()
        => SelectNotificationsBySeverity(NotificationSeverity.Error);
}

#endregion

#region Actions

[DuckyAction]
public record AddNotification(Notification Notification);

[DuckyAction]
public record MarkNotificationAsRead(Guid NotificationId);

[DuckyAction]
public record MarkAllNotificationsAsRead;

#endregion

#region Reducers

public record NotificationsReducers : SliceReducers<NotificationsState>
{
    public NotificationsReducers()
    {
        On<AddNotification>(Reduce);
        On<MarkNotificationAsRead>(Reduce);
        On<MarkAllNotificationsAsRead>(Reduce);
    }

    public override NotificationsState GetInitialState()
        => new()
        {
            Notifications = new ValueCollection<Notification>
            {
                new SuccessNotification("Welcome to Ducky!"),
                new WarningNotification("This is a warning."),
                new ErrorNotification("This is an error.")
            }
        };

    private static NotificationsState Reduce(NotificationsState state, AddNotification action)
        => new() { Notifications = state.Notifications.Add(action.Notification) };

    private static NotificationsState Reduce(NotificationsState state, MarkNotificationAsRead action)
        => new()
        {
            Notifications = state.Notifications
                .Select(n => (n.Id == action.NotificationId)
                    ? n with { IsRead = true }
                    : n)
                .ToValueCollection()
        };

    private static NotificationsState Reduce(NotificationsState state, MarkAllNotificationsAsRead action)
        => new()
        {
            Notifications = state.Notifications
                .Select(n => n with { IsRead = true })
                .ToValueCollection()
        };
}

#endregion
