// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Counter;

public sealed class CounterEffectsTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CompositeDisposable _disposables = new();
    private readonly Subject<IAction> _actionsSubject = new();
    private readonly Subject<RootState> _rootStateSubject = new();
    private IAction? _actualAction;

    public CounterEffectsTests()
    {
        new IncrementEffect { TimeProvider = _timeProvider }
            .Handle(_actionsSubject, _rootStateSubject)
            .Subscribe(action => _actualAction = action)
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _actionsSubject.Dispose();
        _rootStateSubject.Dispose();
    }

    [Fact]
    public void IncrementEffect_ShouldEmitResetAction_WhenValueGreaterThan15()
    {
        // Arrange
        SetupRootState(16);

        // Act
        _actionsSubject.OnNext(new Increment());
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Assert
        _actualAction.Should().BeOfType<Reset>();
    }

    [Fact]
    public void IncrementEffect_ShouldNotEmitResetAction_WhenValueIs15OrLess()
    {
        // Arrange
        SetupRootState(15);

        // Act
        _actionsSubject.OnNext(new Increment());
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Assert
        _actualAction.Should().BeNull();
    }

    private void SetupRootState(int counterValue)
    {
        var dictionary = new Dictionary<string, object> { { "counter", counterValue } };

        var rootState = new RootState(dictionary.ToImmutableSortedDictionary());
        _rootStateSubject.OnNext(rootState);
    }
}
