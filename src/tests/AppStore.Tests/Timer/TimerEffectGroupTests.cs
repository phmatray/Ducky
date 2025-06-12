using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Microsoft.Extensions.Logging;

namespace AppStore.Tests.Timer;

public class TimerEffectGroupTests
{
    private readonly Mock<ILogger<TimerEffectGroup>> _loggerMock = new();
    private readonly Mock<IDispatcher> _dispatcherMock = new();
    private readonly Mock<IRootState> _rootStateMock = new();
    private readonly TimerEffectGroup _effectGroup;

    public TimerEffectGroupTests()
    {
        _effectGroup = new TimerEffectGroup(_loggerMock.Object);
        _effectGroup.SetDispatcher(_dispatcherMock.Object);
    }

    [Fact]
    public void CanHandle_TimerActions_Should_ReturnTrue()
    {
        // Assert
        _effectGroup.CanHandle(new StartTimer()).ShouldBeTrue();
        _effectGroup.CanHandle(new StopTimer()).ShouldBeTrue();
        _effectGroup.CanHandle(new ResetTimer()).ShouldBeTrue();
        _effectGroup.CanHandle(new Tick()).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonTimerAction_Should_ReturnFalse()
    {
        // Arrange
        LoadMovies action = new();

        // Act
        bool result = _effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleStartTimerAsync_Should_StartTimerLoop()
    {
        // Arrange
        StartTimer action = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Act
        _ = _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Wait a bit for the timer to start
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting timer")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStopTimerAsync_Should_StopTimerLoop()
    {
        // Arrange
        StartTimer startAction = new();
        StopTimer stopAction = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Start timer first
        _ = _effectGroup.HandleAsync(startAction, _rootStateMock.Object);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        await _effectGroup.HandleAsync(stopAction, _rootStateMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Stopping timer")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResetTimerAsync_Should_StopTimerAndLogReset()
    {
        // Arrange
        ResetTimer action = new();

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Resetting timer")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleTickAsync_Should_LogTickAndDispatchNotificationEvery10Seconds()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 10 };
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Timer tick: 10s")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<AddNotification>(a =>
                    a.Notification is InfoNotification &&
                    a.Notification.Message == "Timer: 10 seconds")),
            Times.Once);
    }

    [Fact]
    public async Task HandleTickAsync_AtNonInterval_Should_NotDispatchNotification()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 7 }; // Not divisible by 10
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<AddNotification>(a => a.Notification is InfoNotification)),
            Times.Never);
    }

    [Fact]
    public async Task HandleTickAsync_AtZero_Should_NotDispatchNotification()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Assert
        _dispatcherMock.Verify(
            x => x.Dispatch(
                It.Is<AddNotification>(a => a.Notification is InfoNotification)),
            Times.Never);
    }

    [Fact]
    public async Task TimerAsync_Should_AutoStopAt60Seconds()
    {
        // Arrange
        StartTimer action = new();
        TimerState runningState = new() { IsRunning = true, Time = 59 };
        TimerState atLimitState = new() { IsRunning = true, Time = 60 };

        _rootStateMock.SetupSequence(x => x.GetSliceState<TimerState>())
            .Returns(runningState)  // First check when timer starts
            .Returns(runningState)  // During first tick
            .Returns(atLimitState)  // When timer reaches 60 seconds
            .Returns(atLimitState); // Subsequent checks

        // Act
        _ = _effectGroup.HandleAsync(action, _rootStateMock.Object);

        // Wait for timer to process multiple ticks
        await Task.Delay(3000, TestContext.Current.CancellationToken);

        // Assert
        _dispatcherMock.Verify(x => x.Dispatch(It.IsAny<StopTimer>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task MultipleStartTimerAsync_Should_CancelPreviousTimer()
    {
        // Arrange
        StartTimer action = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        _rootStateMock.Setup(x => x.GetSliceState<TimerState>()).Returns(timerState);

        // Act
        _ = _effectGroup.HandleAsync(action, _rootStateMock.Object);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _ = _effectGroup.HandleAsync(action, _rootStateMock.Object);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Starting timer")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TimerAsync_WhenStopped_Should_NotDispatchTicks()
    {
        // Arrange
        StartTimer startAction = new();
        StopTimer stopAction = new();
        TimerState runningState = new() { IsRunning = true, Time = 0 };
        TimerState stoppedState = new() { IsRunning = false, Time = 1 };

        _rootStateMock.SetupSequence(x => x.GetSliceState<TimerState>())
            .Returns(runningState)  // When timer starts
            .Returns(stoppedState); // After timer is stopped

        // Act - Start timer then immediately stop it
        _ = _effectGroup.HandleAsync(startAction, _rootStateMock.Object);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Brief delay to let timer start
        await _effectGroup.HandleAsync(stopAction, _rootStateMock.Object);
        await Task.Delay(1500, TestContext.Current.CancellationToken); // Wait to ensure no ticks after stop

        // Assert
        _dispatcherMock.Verify(x => x.Dispatch(It.IsAny<Tick>()), Times.Never);
    }
}
