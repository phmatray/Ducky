// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Timer;

public sealed class TimerEffectsTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<IAction> _actionsSubject = new();
    private readonly Subject<IRootState> _rootStateSubject = new();
    private readonly List<IAction> _actualActions = [];

    public TimerEffectsTests()
    {
        new StartTimerEffect { TimeProvider = _timeProvider }
            .Handle(_actionsSubject, _rootStateSubject)
            .Subscribe(_actualActions.Add)
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
        var dictionary = new Dictionary<string, object> { { "timer", 0 } };

        RootState rootState = new(dictionary.ToImmutableSortedDictionary());
        _rootStateSubject.OnNext(rootState);
    }
}
