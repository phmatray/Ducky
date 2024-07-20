// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.Extensions.Operators;

public sealed class CustomOperatorsTests : IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private bool _disposed;

    [Fact]
    public void FilterActions_Should_FilterByType()
    {
        // Arrange
        var source = new Subject<IAction>();
        var actions = new List<IAction>();

        source.AsObservable()
            .FilterActions<TestAction>()
            .Subscribe(actions.Add)
            .AddTo(_disposables);

        // Act
        source.OnNext(new TestAction());
        source.OnNext(new OtherAction());

        // Assert
        actions.Should().HaveCount(1);
        actions.First().Should().BeOfType<TestAction>();
    }

    [Fact]
    public void SelectAction_Should_TransformAndCast()
    {
        // Arrange
        var source = new Subject<int>();
        var actions = new List<IAction>();

        source.AsObservable()
            .SelectAction<int, TestAction>(_ => new TestAction())
            .Subscribe(actions.Add)
            .AddTo(_disposables);

        // Act
        source.OnNext(1);
        source.OnNext(2);

        // Assert
        actions.Should().HaveCount(2);
        actions.Should().AllBeOfType<TestAction>();
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
    //     results.Should().HaveCount(3);
    //     results.Should().HaveElementAt(0, 1);
    //     results.Should().HaveElementAt(1, -1);
    //     results.Should().HaveElementAt(2, 2);
    // }
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _disposables.Dispose();
            }

            _disposed = true;
        }
    }
}
