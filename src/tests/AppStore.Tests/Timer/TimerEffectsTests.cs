// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace AppStore.Tests.Timer;

public sealed class TimerEffectsTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<object> _actionsSubject = new();
    private readonly Subject<IRootState> _rootStateSubject = new();
    private readonly List<object> _actualActions = [];

    public TimerEffectsTests()
    {
        ObservableSystem.DefaultTimeProvider = _timeProvider;

        new StartTimerEffect()
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
        _actualActions.Count.ShouldBe(3);
        foreach (object action in _actualActions)
        {
            action.ShouldBeOfType<Tick>();
        }
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
        _actualActions.Count.ShouldBe(2);
        foreach (object action in _actualActions)
        {
            action.ShouldBeOfType<Tick>();
        }
    }

    private void SetupRootState()
    {
        ImmutableSortedDictionary<string, object> dictionary = ImmutableSortedDictionary
            .Create<string, object>()
            .Add("timer", 0);

        RootState rootState = new(dictionary);
        _rootStateSubject.OnNext(rootState);
    }
}
