using System.Text;
using R3dux.Temp;
using R3dux.Tests.TestModels;

namespace R3dux.Tests;

public class CustomOperatorsTests
{
    private record TestAction(int Value = 0) : IAction;

    private record OtherAction : IAction;

    private record SuccessAction(int Result) : IAction;

    private record ErrorAction(string ErrorMessage) : IAction;

    [Fact]
    public void FilterActions_Should_FilterByType()
    {
        // Arrange
        var source = new Subject<IAction>();
        var observable = source.AsObservable();

        var actions = new List<IAction>();
        observable.FilterActions<TestAction>().Subscribe(actions.Add);

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
        var observable = source.AsObservable();

        var actions = new List<IAction>();
        observable.SelectAction<int, TestAction>(i => new TestAction { Value = i }).Subscribe(actions.Add);

        // Act
        source.OnNext(1);
        source.OnNext(2);

        // Assert
        actions.Should().HaveCount(2);
        actions.Should().AllBeOfType<TestAction>();
    }

    [Fact]
    public void CatchAction_Should_CatchExceptionsAndReplace()
    {
        // Arrange
        var source = new Subject<int>();
        var observable = source.AsObservable();

        var values = new List<int>();
        observable.CatchAction(ex => -1).Subscribe(values.Add);

        // Act
        source.OnNext(1);
        source.OnErrorResume(new Exception());
        source.OnNext(2);

        // Assert
        values.Count.Should().Be(2);
        values.Should().ContainInOrder(1, -1);
    }
}