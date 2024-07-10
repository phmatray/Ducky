using System.Collections.Immutable;
using R3;

namespace R3dux.Tests.Extensions;

public class CustomOperatorsTests
{
    private record TestAction(int Value = 0) : IAction;

    private record OtherAction : IAction;

    private record SuccessAction(int Result) : IAction;

    private record ErrorAction(string ErrorMessage) : IAction;
    
    private record StateActionPair<TState, TAction>(TState State, TAction Action)
        where TState : notnull
        where TAction : IAction;
    
    private readonly CompositeDisposable _disposables = new();

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
            .SelectAction<int, TestAction>(i => new TestAction { Value = i })
            .Subscribe(actions.Add)
            .AddTo(_disposables);;

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
}