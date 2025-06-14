using Bunit;
using Microsoft.AspNetCore.Components.Rendering;
using FakeItEasy;
using System.Collections.Immutable;

namespace Ducky.Blazor.Tests;

public class DuckyComponentTests : Bunit.TestContext
{
    private readonly IStore _storeMock;
    private readonly IDispatcher _dispatcherMock;
    private readonly RootState _rootState;

    public DuckyComponentTests()
    {
        _storeMock = A.Fake<IStore>();
        _dispatcherMock = A.Fake<IDispatcher>();

        // Create a root state with some test data
        ImmutableSortedDictionary<string, object> stateDict =
            ImmutableSortedDictionary<string, object>.Empty
                .Add("test", new TestState { Value = 42 });
        _rootState = new RootState(stateDict);

        // Note: Can't mock CurrentState() as it's an extension method
        // But we can mock GetSlice for specific state types
        A.CallTo(() => _storeMock.GetSlice<TestState>()).Returns(new TestState { Value = 42 });

        Services.AddSingleton(_storeMock);
        Services.AddSingleton(_dispatcherMock);
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
        // Update the mocked GetSlice to return new value
        A.CallTo(() => _storeMock.GetSlice<TestState>()).Returns(new TestState { Value = 100 });

        _storeMock.StateChanged += Raise.FreeForm<EventHandler<StateChangedEventArgs>>
            .With(
                _storeMock,
                new StateChangedEventArgs(
                    "test",
                    typeof(TestState),
                    new TestState { Value = 100 },
                    new TestState { Value = 42 }));

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
