using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using Moq;
using Shouldly;
using System.Collections.Immutable;

namespace Ducky.Blazor.Tests;

public class DuckyComponentTests : Bunit.TestContext
{
    private readonly Mock<IStore> _storeMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly RootState _rootState;

    public DuckyComponentTests()
    {
        _storeMock = new Mock<IStore>();
        _dispatcherMock = new Mock<IDispatcher>();

        // Create a root state with some test data
        ImmutableSortedDictionary<string, object> stateDict =
            ImmutableSortedDictionary<string, object>.Empty
                .Add("test", new TestState { Value = 42 });
        _rootState = new RootState(stateDict);

        _storeMock.Setup(s => s.CurrentState).Returns(_rootState);

        Services.AddSingleton(_storeMock.Object);
        Services.AddSingleton(_dispatcherMock.Object);
        Services.AddLogging();
    }

    [Fact]
    public void DuckyComponent_WithRootState_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        IRenderedComponent<TestRootStateComponent> component = RenderComponent<TestRootStateComponent>();

        // Verify the component rendered without throwing
        component.Markup.ShouldContain("Root State Component");
    }

    [Fact]
    public void DuckyComponent_WithSliceState_ShouldRetrieveCorrectSlice()
    {
        // Act
        IRenderedComponent<TestSliceComponent> component = RenderComponent<TestSliceComponent>();

        // Assert
        component.Markup.ShouldContain("Value: 42");
    }

    [Fact]
    public void DuckyComponent_StateChange_ShouldTriggerRerender()
    {
        // Arrange
        IRenderedComponent<TestSliceComponent> component = RenderComponent<TestSliceComponent>();
        int renderCount = component.RenderCount;

        // Act - simulate state change
        ImmutableSortedDictionary<string, object> newStateDict =
            ImmutableSortedDictionary<string, object>.Empty
                .Add("test", new TestState { Value = 100 });
        RootState newRootState = new(newStateDict);
        _storeMock.Setup(s => s.CurrentState).Returns(newRootState);

        _storeMock.Raise(
            s => s.StateChanged += null,
            new StateChangedEventArgs(_rootState, newRootState));

        // Assert
        component.RenderCount.ShouldBeGreaterThan(renderCount);
        component.Markup.ShouldContain("Value: 100");
    }

    // Test components
    private class TestRootStateComponent : DuckyComponent<RootState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, "Root State Component");
        }
    }

    private class TestSliceComponent : DuckyComponent<TestState>
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, $"Value: {State.Value}");
        }
    }

    private record TestState : IState
    {
        public int Value { get; init; }
    }
}
