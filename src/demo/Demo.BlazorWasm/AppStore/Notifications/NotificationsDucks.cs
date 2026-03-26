// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

#region State

public record NotificationsState
{
    public required ImmutableArray<Notification> Notifications { get; init; }

    // Selectors
    public ImmutableArray<Notification> SelectUnreadNotifications()
        =>
        [
            ..Notifications
                .Where(n => !n.IsRead)
                .Reverse()
        ];

    public bool SelectHasUnreadNotifications()
        => !SelectUnreadNotifications().IsEmpty;

    public int SelectUnreadNotificationCount()
        => SelectUnreadNotifications().Length;

    public ImmutableArray<Notification> SelectNotificationsBySeverity(NotificationSeverity severity)
        => SelectUnreadNotifications()
            .Where(n => n.Severity == severity)
            .ToImmutableArray();

    public ImmutableArray<Notification> SelectErrorNotifications()
        => SelectNotificationsBySeverity(NotificationSeverity.Error);
}

#endregion

#region Actions

[DuckyAction]
public partial record AddNotification(Notification Notification);

[DuckyAction]
public partial record MarkNotificationAsRead(Guid NotificationId);

[DuckyAction]
public partial record MarkAllNotificationsAsRead;

[DuckyAction]
public partial record ClearErrorNotifications;

#endregion

#region Reducers

public record NotificationsReducers : SliceReducers<NotificationsState>
{
    public NotificationsReducers()
    {
        On<AddNotification>(Reduce);
        On<MarkNotificationAsRead>(Reduce);
        On<MarkAllNotificationsAsRead>(Reduce);
        On<ClearErrorNotifications>(Reduce);
    }

    public override NotificationsState GetInitialState()
    {
        Notification[] initial =
        [
            new SuccessNotification("Welcome to Ducky!"),
            new WarningNotification("This is a warning."),
            new ErrorNotification("This is an error.")
        ];
        return new NotificationsState { Notifications = initial.ToImmutableArray() };
    }

    private static NotificationsState Reduce(NotificationsState state, AddNotification action)
        => new() { Notifications = state.Notifications.Add(action.Notification) };

    private static NotificationsState Reduce(NotificationsState state, MarkNotificationAsRead action)
        => new()
        {
            Notifications = state.Notifications
                .Select(n => n.Id == action.NotificationId
                    ? n with { IsRead = true }
                    : n)
                .ToImmutableArray()
        };

    private static NotificationsState Reduce(NotificationsState state, MarkAllNotificationsAsRead action)
        => new()
        {
            Notifications = state.Notifications
                .Select(n => n with { IsRead = true })
                .ToImmutableArray()
        };

    private static NotificationsState Reduce(NotificationsState state, ClearErrorNotifications action)
        => new()
        {
            Notifications = state.Notifications
                .Where(n => n.Severity != NotificationSeverity.Error)
                .ToImmutableArray()
        };
}

#endregion
