using Demo.BlazorWasm.AppStore;
using Demo.BlazorWasm.Features.Feedback.Effects;
using Microsoft.Extensions.Logging;

namespace AppStore.Tests.Timer;

public class TimerEffectGroupTests
{
    private readonly ILogger<TimerEffectGroup> _logger = A.Fake<ILogger<TimerEffectGroup>>();
    private readonly IDispatcher _dispatcher = A.Fake<IDispatcher>();
    private readonly IStateProvider _stateProvider = A.Fake<IStateProvider>();
    private readonly TimerEffectGroup _effectGroup;

    public TimerEffectGroupTests()
    {
        _effectGroup = new TimerEffectGroup(_logger);
        _effectGroup.SetDispatcher(_dispatcher);
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
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Act
        _ = _effectGroup.HandleAsync(action, _stateProvider);

        // Wait a bit for the timer to start
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_logger).Where(call => call.Method.Name == "Log"
            && call.GetArgument<LogLevel>(0) == LogLevel.Information
            && call.GetArgument<object>(2)!.ToString()!.Contains("Starting timer"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleStopTimerAsync_Should_StopTimerLoop()
    {
        // Arrange
        StartTimer startAction = new();
        StopTimer stopAction = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Start timer first
        _ = _effectGroup.HandleAsync(startAction, _stateProvider);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Act
        await _effectGroup.HandleAsync(stopAction, _stateProvider);

        // Assert
        A.CallTo(_logger).Where(call => call.Method.Name == "Log"
            && call.GetArgument<LogLevel>(0) == LogLevel.Information
            && call.GetArgument<object>(2)!.ToString()!.Contains("Stopping timer"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleResetTimerAsync_Should_StopTimerAndLogReset()
    {
        // Arrange
        ResetTimer action = new();

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(_logger).Where(call => call.Method.Name == "Log"
            && call.GetArgument<LogLevel>(0) == LogLevel.Information
            && call.GetArgument<object>(2)!.ToString()!.Contains("Resetting timer"))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleTickAsync_Should_LogTickAndDispatchNotificationEvery10Seconds()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 10 };
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(_logger).Where(call => call.Method.Name == "Log"
            && call.GetArgument<LogLevel>(0) == LogLevel.Debug
            && call.GetArgument<object>(2)!.ToString()!.Contains("Timer tick: 10s"))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _dispatcher.Dispatch(
            A<AddNotification>.That.Matches(a =>
                a.Notification is InfoNotification &&
                a.Notification.Message == "Timer: 10 seconds")))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleTickAsync_AtNonInterval_Should_NotDispatchNotification()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 7 }; // Not divisible by 10
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<AddNotification>.That.Matches(a => a.Notification is InfoNotification)))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleTickAsync_AtZero_Should_NotDispatchNotification()
    {
        // Arrange
        Tick action = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Act
        await _effectGroup.HandleAsync(action, _stateProvider);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(
            A<AddNotification>.That.Matches(a => a.Notification is InfoNotification)))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task TimerAsync_Should_AutoStopAt60Seconds()
    {
        // Arrange
        StartTimer action = new();
        TimerState runningState = new() { IsRunning = true, Time = 59 };
        TimerState atLimitState = new() { IsRunning = true, Time = 60 };

        A.CallTo(() => _stateProvider.GetSlice<TimerState>())
            .ReturnsNextFromSequence(
                runningState,  // First check when timer starts
                runningState,  // During first tick
                atLimitState,  // When timer reaches 60 seconds
                atLimitState); // Subsequent checks

        // Act
        _ = _effectGroup.HandleAsync(action, _stateProvider);

        // Wait for timer to process multiple ticks
        await Task.Delay(3000, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(A<StopTimer>.Ignored))
            .MustHaveHappened();
    }

    [Fact]
    public async Task MultipleStartTimerAsync_Should_CancelPreviousTimer()
    {
        // Arrange
        StartTimer action = new();
        TimerState timerState = new() { IsRunning = true, Time = 0 };
        A.CallTo(() => _stateProvider.GetSlice<TimerState>()).Returns(timerState);

        // Act
        _ = _effectGroup.HandleAsync(action, _stateProvider);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _ = _effectGroup.HandleAsync(action, _stateProvider);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(_logger).Where(call => call.Method.Name == "Log"
            && call.GetArgument<LogLevel>(0) == LogLevel.Information
            && call.GetArgument<object>(2)!.ToString()!.Contains("Starting timer"))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task TimerAsync_WhenStopped_Should_NotDispatchTicks()
    {
        // Arrange
        StartTimer startAction = new();
        StopTimer stopAction = new();
        TimerState runningState = new() { IsRunning = true, Time = 0 };
        TimerState stoppedState = new() { IsRunning = false, Time = 1 };

        A.CallTo(() => _stateProvider.GetSlice<TimerState>())
            .ReturnsNextFromSequence(
                runningState,  // When timer starts
                stoppedState); // After timer is stopped

        // Act - Start timer then immediately stop it
        _ = _effectGroup.HandleAsync(startAction, _stateProvider);
        await Task.Delay(100, TestContext.Current.CancellationToken); // Brief delay to let timer start
        await _effectGroup.HandleAsync(stopAction, _stateProvider);
        await Task.Delay(1500, TestContext.Current.CancellationToken); // Wait to ensure no ticks after stop

        // Assert
        A.CallTo(() => _dispatcher.Dispatch(A<Tick>.Ignored))
            .MustNotHaveHappened();
    }
}
