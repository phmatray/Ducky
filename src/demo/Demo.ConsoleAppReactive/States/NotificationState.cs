using System.Collections.Immutable;

namespace Demo.ConsoleAppReactive.States;

// Notification state for workflow effect demo
public record NotificationState : IState
{
    public ImmutableList<Notification> Notifications { get; init; } = ImmutableList<Notification>.Empty;
    public int ProcessedCount { get; init; }
}

public record Notification(
    Guid Id,
    string Title,
    string Message,
    NotificationType Type,
    DateTime CreatedAt);

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}

// Notification actions
[DuckyAction]
public record StartNotificationWorkflow(string Title, string Message, NotificationType Type);

[DuckyAction]
public record NotificationCreated(Guid Id, string Title, string Message, NotificationType Type);

[DuckyAction]
public record NotificationProcessed(Guid Id);

[DuckyAction]
public record SimulateError(string ErrorType);

// Notification slice reducers
public record NotificationSliceReducers : SliceReducers<NotificationState>
{
    public override NotificationState GetInitialState() => new();

    public NotificationSliceReducers()
    {
        On<NotificationCreated>((state, action) =>
        {
            Notification notification = new(
                action.Id,
                action.Title,
                action.Message,
                action.Type,
                DateTime.UtcNow);

            return state with
            {
                Notifications = state.Notifications.Add(notification)
            };
        });

        On<NotificationProcessed>((state, action) => state with
            {
                ProcessedCount = state.ProcessedCount + 1
            });
    }
}
