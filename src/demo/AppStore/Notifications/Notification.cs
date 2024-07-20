// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore;

public abstract record Notification(
    string Message,
    NotificationSeverity Severity)
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public bool IsRead { get; init; }
}

public record InfoNotification(string Message)
    : Notification(Message, NotificationSeverity.Info);

public record SuccessNotification(string Message)
    : Notification(Message, NotificationSeverity.Success);

public record WarningNotification(string Message)
    : Notification(Message, NotificationSeverity.Warning);

public record ErrorNotification(string Message)
    : Notification(Message, NotificationSeverity.Error);

public record ExceptionNotification : Notification
{
    private readonly Exception _exception;

    public ExceptionNotification(Exception ex)
        : base(ex.Message, NotificationSeverity.Error)
    {
        _exception = ex;
    }

    public string? StackTrace
        => _exception.StackTrace;

    public string? Source
        => _exception.Source;
}
