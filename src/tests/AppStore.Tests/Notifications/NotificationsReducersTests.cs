// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Notifications;

public sealed class NotificationsReducersTests : IDisposable
{
    private const string Key = "notifications";

    private readonly NotificationsReducers _sut = new();

    private readonly NotificationsState _initialState = new()
    {
        Notifications = new ValueCollection<Notification>
        {
            new SuccessNotification("Welcome to Ducky!"),
            new WarningNotification("This is a warning."),
            new ErrorNotification("This is an error.")
        }
    };

    private bool _disposed;

    [Fact]
    public void NotificationsReducers_Should_Return_Initial_State()
    {
        // Act
        NotificationsState initialState = _sut.GetInitialState();

        // Assert
        initialState.Notifications.Should().HaveCount(3);
        initialState.Notifications[0].Message.Should().Be("Welcome to Ducky!");
        initialState.Notifications[1].Message.Should().Be("This is a warning.");
        initialState.Notifications[2].Message.Should().Be("This is an error.");
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<NotificationsState>();
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<NotificationsState, IAction, NotificationsState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(3);
    }

    [Fact]
    public void AddNotification_Should_Add_Notification()
    {
        // Arrange
        InfoNotification notification = new("This is an info notification.");
        AddNotification action = new(notification);

        // Act
        NotificationsState state = _sut.Reduce(_initialState, action);

        // Assert
        state.Notifications.Should().HaveCount(4);
        state.Notifications.Should().HaveElementAt(3, notification);
    }

    [Fact]
    public void MarkNotificationAsRead_Should_Mark_Notification_As_Read()
    {
        // Arrange
        Guid notificationId = _initialState.Notifications[1].Id;
        MarkNotificationAsRead action = new(notificationId);

        // Act
        NotificationsState state = _sut.Reduce(_initialState, action);

        // Assert
        state.Notifications[1].IsRead.Should().BeTrue();
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}
