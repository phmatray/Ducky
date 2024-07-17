using System.Collections.Immutable;
using Microsoft.Extensions.Time.Testing;
using R3;
using R3dux;

namespace Demo.AppStore.Tests.Timer;

public sealed class TimerEffectsTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CompositeDisposable _disposables = new();
    private readonly Subject<IAction> _actionsSubject = new();
    private readonly Subject<RootState> _rootStateSubject = new();
    private readonly List<IAction> _actualActions = [];

    public TimerEffectsTests()
    {
        new StartTimerEffect { TimeProvider = _timeProvider }
            .Handle(_actionsSubject, _rootStateSubject)
            .Subscribe(action => _actualActions.Add(action))
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _actionsSubject.Dispose();
        _rootStateSubject.Dispose();
    }

    [Fact]
    public void StartTimerEffect_ShouldEmitTickActions_WhenTimerStarted()
    {
        // Arrange
        SetupRootState();

        // Act
        _actionsSubject.OnNext(new StartTimer());
        _timeProvider.Advance(TimeSpan.FromSeconds(3));

        // Assert
        _actualActions.Should().HaveCount(3);
        _actualActions.Should().AllBeOfType<Tick>();
    }

    [Fact]
    public void StartTimerEffect_ShouldStopEmittingTickActions_WhenTimerStopped()
    {
        // Arrange
        SetupRootState();

        // Act
        _actionsSubject.OnNext(new StartTimer());
        _timeProvider.Advance(TimeSpan.FromSeconds(2));
        _actionsSubject.OnNext(new StopTimer());
        _timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Assert
        _actualActions.Should().HaveCount(2);
        _actualActions.Should().AllBeOfType<Tick>();
    }

    private void SetupRootState()
    {
        var dictionary = new Dictionary<string, object>
        {
            { "timer", 0 }
        };

        var rootState = new RootState(dictionary.ToImmutableSortedDictionary());
        _rootStateSubject.OnNext(rootState);
    }
}