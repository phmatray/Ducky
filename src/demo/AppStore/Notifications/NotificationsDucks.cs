// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Notifications;

#region State

public record NotificationsState
{
    public required ImmutableList<Notification> Notifications { get; init; }

    // Selectors
    public ImmutableList<Notification> SelectUnreadNotifications()
    {
        return Notifications
            .Where(n => !n.IsRead)
            .Reverse()
            .ToImmutableList();
    }

    public bool SelectHasUnreadNotifications()
    {
        return !SelectUnreadNotifications().IsEmpty;
    }

    public int SelectUnreadNotificationCount()
    {
        return SelectUnreadNotifications().Count;
    }

    public ImmutableList<Notification> SelectNotificationsBySeverity(
        NotificationSeverity severity)
    {
        return SelectUnreadNotifications()
            .Where(n => n.Severity == severity)
            .ToImmutableList();
    }

    public ImmutableList<Notification> SelectErrorNotifications()
    {
        return SelectNotificationsBySeverity(NotificationSeverity.Error);
    }
}

#endregion

#region Actions

public record AddNotification(Notification Notification) : IAction;

public record MarkNotificationAsRead(Guid NotificationId) : IAction;

public record MarkAllNotificationsAsRead : IAction;

#endregion

#region Reducers

public record NotificationsReducers : SliceReducers<NotificationsState>
{
    public NotificationsReducers()
    {
        On<AddNotification>(ReduceAddNotification);
        On<MarkNotificationAsRead>(ReduceMarkNotificationAsRead);
        On<MarkAllNotificationsAsRead>(ReduceMarkAllNotificationsAsRead);
    }

    public override NotificationsState GetInitialState()
    {
        return new NotificationsState
        {
            Notifications =
            [
                new SuccessNotification("Welcome to Ducky!"),
                new WarningNotification("This is a warning."),
                new ErrorNotification("This is an error.")
            ]
        };
    }

    private static NotificationsState ReduceAddNotification(
        NotificationsState state, AddNotification action)
    {
        return new NotificationsState { Notifications = state.Notifications.Add(action.Notification) };
    }

    private static NotificationsState ReduceMarkNotificationAsRead(
        NotificationsState state, MarkNotificationAsRead action)
    {
        return new NotificationsState
        {
            Notifications = state.Notifications
                .Select(n => n.Id == action.NotificationId
                    ? n with { IsRead = true }
                    : n)
                .ToImmutableList()
        };
    }

    private static NotificationsState ReduceMarkAllNotificationsAsRead(
        NotificationsState state, MarkAllNotificationsAsRead action)
    {
        return new NotificationsState
        {
            Notifications = state.Notifications
                .Select(n => n with { IsRead = true })
                .ToImmutableList()
        };
    }
}

#endregion
