namespace Demo.AppStore.Tests.Notifications;

public class NotificationsReducersTests
{
    private readonly NotificationsReducers _sut = new();

    private readonly NotificationsState _initialState = new()
    {
        Notifications =
        [
            new SuccessNotification("Welcome to R3dux!"),
            new WarningNotification("This is a warning."),
            new ErrorNotification("This is an error.")
        ]
    };
    
    private const string Key = "notifications";
    
    [Fact]
    public void NotificationsReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Notifications.Should().HaveCount(3);
        initialState.Notifications[0].Message.Should().Be("Welcome to R3dux!");
        initialState.Notifications[1].Message.Should().Be("This is a warning.");
        initialState.Notifications[2].Message.Should().Be("This is an error.");
    }
    
    [Fact]
    public void NotificationsReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }
    
    [Fact]
    public void NotificationsReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(NotificationsState));
    }
    
    [Fact]
    public void NotificationsReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(2);
    }
    
    [Fact]
    public void AddNotification_Should_Add_Notification()
    {
        // Arrange
        var notification = new InfoNotification("This is an info notification.");
        var action = new AddNotification(notification);
        
        // Act
        var state = _sut.Reduce(_initialState, action);

        // Assert
        state.Notifications.Should().HaveCount(4);
        state.Notifications[3].Should().Be(notification);
    }
    
    [Fact]
    public void MarkNotificationAsRead_Should_Mark_Notification_As_Read()
    {
        // Arrange
        var notificationId = _initialState.Notifications[1].Id;
        var action = new MarkNotificationAsRead(notificationId);
        
        // Act
        var state = _sut.Reduce(_initialState, action);

        // Assert
        state.Notifications[1].IsRead.Should().BeTrue();
    }
}