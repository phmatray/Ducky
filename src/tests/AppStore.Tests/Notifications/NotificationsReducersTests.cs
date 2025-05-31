// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Notifications;

public sealed class NotificationsReducersTests : IDisposable
{
    private const string Key = "demo-blazor-wasm-app-store-notifications";

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
        initialState.Notifications.Count.ShouldBe(3);
        initialState.Notifications[0].Message.ShouldBe("Welcome to Ducky!");
        initialState.Notifications[1].Message.ShouldBe("This is a warning.");
        initialState.Notifications[2].Message.ShouldBe("This is an error.");
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.ShouldBe(Key);
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.FullName.ShouldBe(typeof(NotificationsState).FullName);
    }

    [Fact]
    public void NotificationsReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<NotificationsState, object, NotificationsState>> reducers = _sut.Reducers;

        // Assert
        reducers.Count.ShouldBe(4);
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
        state.Notifications.Count.ShouldBe(4);
        state.Notifications[3].Message.ShouldBe("This is an info notification.");
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
        state.Notifications[1].IsRead.ShouldBeTrue();
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
