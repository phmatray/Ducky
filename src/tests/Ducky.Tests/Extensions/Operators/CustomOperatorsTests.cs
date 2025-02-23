// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Extensions.Operators;

public sealed class CustomOperatorsTests : IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private bool _disposed;

    [Fact]
    public void FilterActions_Should_FilterByType()
    {
        // Arrange
        Subject<object> source = new();
        List<object> actions = [];

        source.AsObservable()
            .OfActionType<TestAction>()
            .Subscribe(actions.Add)
            .AddTo(_disposables);

        // Act
        source.OnNext(new TestAction());
        source.OnNext(new OtherAction());

        // Assert
        actions.ShouldHaveSingleItem();
        actions[0].ShouldBeOfType<TestAction>();
    }

    [Fact]
    public void SelectAction_Should_TransformAndCast()
    {
        // Arrange
        Subject<int> source = new();
        List<object> actions = [];

        source.AsObservable()
            .SelectAction<int, TestAction>(_ => new TestAction())
            .Subscribe(actions.Add)
            .AddTo(_disposables);

        // Act
        source.OnNext(1);
        source.OnNext(2);

        // Assert
        actions.Count.ShouldBe(2);
        foreach (object action in actions)
        {
            action.ShouldBeOfType<TestAction>();
        }
    }

    // TODO: Not sure if the assertion is correct
    // [Fact]
    // public void CatchAction_Should_HandleErrors()
    // {
    //     // Arrange
    //     var source = new Subject<int>();
    //     var results = new List<int>();
    //
    //     source.AsObservable()
    //         .CatchAction((Exception ex) => -1)
    //         .Subscribe(results.Add)
    //         .AddTo(_disposables);
    //
    //     // Act
    //     source.OnNext(1);
    //     source.OnErrorResume(new Exception("Test exception"));
    //     source.OnNext(2);
    //
    //     // Assert
    //     results.ShouldBe(3);
    //     results.ShouldHaveElementAt(0, 1);
    //     results.ShouldHaveElementAt(1, -1);
    //     results.ShouldHaveElementAt(2, 2);
    // }
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
            _disposables.Dispose();
        }

        _disposed = true;
    }
}
