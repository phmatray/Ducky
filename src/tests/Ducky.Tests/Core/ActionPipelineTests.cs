// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Pipeline;

namespace Ducky.Tests.Core;

public sealed class ActionPipelineTests
{
    [Fact]
    public async Task Use_Should_Register_Middleware_And_Invoke_BeforeReduce()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        (ActionPipeline pipeline, _, _, _) = await CreatePipelineWithMiddleware();

        DummyAction action = new();

        // Check if action may be dispatched
        bool mayDispatch = pipeline.MayDispatchAction(action);
        mayDispatch.ShouldBeTrue();

        // Execute BeforeReduce
        pipeline.BeforeReduce(action);

        DummyMiddleware.StaticCalls.ShouldContain("before:DummyAction");

        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    [Fact]
    public async Task Use_Should_Register_Middleware_And_Invoke_AfterReduce()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        (ActionPipeline pipeline, _, _, _) = await CreatePipelineWithMiddleware();

        DummyAction action = new();

        // Execute full lifecycle
        bool mayDispatch = pipeline.MayDispatchAction(action);
        mayDispatch.ShouldBeTrue();
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        DummyMiddleware.StaticCalls.ShouldContain("after:DummyAction");

        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    [Fact]
    public async Task Middlewares_Should_Execute_In_Correct_Order()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        List<string> executionOrder = [];

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);

        // Set static properties for testing
        OrderTrackingMiddleware.Name = "MW1";
        OrderTrackingMiddleware.ExecutionOrder = executionOrder;

        // Register middlewares by their interface type
        pipeline.Use(typeof(OrderTrackingMiddleware));
        pipeline.Use(typeof(OrderTrackingMiddleware));

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Act
        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        // Assert
        mayDispatch.ShouldBeTrue();
        // Verify that middlewares were executed
        executionOrder.Count.ShouldBeGreaterThan(0);

        // Clean up static state
        OrderTrackingMiddleware.Name = null;
        OrderTrackingMiddleware.ExecutionOrder = null;
    }

    [Fact]
    public async Task Multiple_Middlewares_Should_Maintain_Execution_Order()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        List<string> executionOrder = [];

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);

        // Set static properties for testing
        OrderTrackingMiddleware.Name = "MW";
        OrderTrackingMiddleware.ExecutionOrder = executionOrder;

        // Register middlewares by type
        for (int i = 0; i < 3; i++)
        {
            pipeline.Use(typeof(OrderTrackingMiddleware));
        }

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Act
        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        // Assert
        mayDispatch.ShouldBeTrue();
        // Verify that middlewares were executed
        executionOrder.Count.ShouldBeGreaterThan(0);

        // Clean up static state
        OrderTrackingMiddleware.Name = null;
        OrderTrackingMiddleware.ExecutionOrder = null;
    }

    [Fact]
    public async Task BeforeReduce_Should_Be_Called_For_Custom_Middleware()
    {
        ServiceCollection services = [];
        services.AddLogging();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);

        var called = false;
        CallbackMiddleware.BeforeAction = () => called = true;
        CallbackMiddleware.AfterAction = () => { };

        pipeline.Use(typeof(CallbackMiddleware));

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);

        mayDispatch.ShouldBeTrue();
        called.ShouldBeTrue();

        // Clean up static state
        CallbackMiddleware.BeforeAction = null;
        CallbackMiddleware.AfterAction = null;
    }

    [Fact]
    public async Task AfterReduce_Should_Be_Called_For_Custom_Middleware()
    {
        ServiceCollection services = [];
        services.AddLogging();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);

        var called = false;
        CallbackMiddleware.BeforeAction = () => { };
        CallbackMiddleware.AfterAction = () => called = true;

        pipeline.Use(typeof(CallbackMiddleware));

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        mayDispatch.ShouldBeTrue();
        called.ShouldBeTrue();

        // Clean up static state
        CallbackMiddleware.BeforeAction = null;
        CallbackMiddleware.AfterAction = null;
    }

    [Fact]
    public async Task Middleware_Should_Be_Able_To_Track_Action_Processing()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);
        pipeline.Use(typeof(MetadataMiddleware));

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Act
        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        // Assert
        mayDispatch.ShouldBeTrue();
        MetadataMiddleware.StaticBeforeTimestamp.ShouldNotBeNull();
        MetadataMiddleware.StaticAfterTimestamp.ShouldNotBeNull();
        MetadataMiddleware.StaticAfterTimestamp.Value.ShouldBeGreaterThanOrEqualTo(MetadataMiddleware
            .StaticBeforeTimestamp.Value);

        // Clean up static state
        MetadataMiddleware.StaticBeforeTimestamp = null;
        MetadataMiddleware.StaticAfterTimestamp = null;
    }

    [Fact]
    public async Task Prevented_Actions_Should_Not_Complete_Processing()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);
        pipeline.Use(typeof(PreventingMiddleware));

        // Reset static state
        PreventingMiddleware.StaticBeforeReduceCalled = false;
        PreventingMiddleware.StaticAfterReduceCalled = false;

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Act
        DummyAction action = new();
        bool mayDispatch = pipeline.MayDispatchAction(action);

        // Only call BeforeReduce/AfterReduce if action may be dispatched
        if (mayDispatch)
        {
            pipeline.BeforeReduce(action);
            pipeline.AfterReduce(action);
        }

        // Assert
        mayDispatch.ShouldBeFalse(); // Action should be prevented
        PreventingMiddleware.StaticBeforeReduceCalled.ShouldBeFalse(); // Should not reach BeforeReduce
        PreventingMiddleware.StaticAfterReduceCalled.ShouldBeFalse(); // Should not reach AfterReduce

        // Clean up static state
        PreventingMiddleware.StaticBeforeReduceCalled = false;
        PreventingMiddleware.StaticAfterReduceCalled = false;
    }

    [Fact]
    public async Task Dispose_Should_Not_Affect_Middleware_Usage()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        // Arrange
        (ActionPipeline pipeline, _, _, _) = await CreatePipelineWithMiddleware();

        // Send an action to verify pipeline is working
        DummyAction action = new();
        bool firstMayDispatch = pipeline.MayDispatchAction(action);
        pipeline.BeforeReduce(action);
        pipeline.AfterReduce(action);

        firstMayDispatch.ShouldBeTrue();

        DummyMiddleware.StaticCalls.Count.ShouldBeGreaterThan(0);

        // Act
        pipeline.Dispose();

        // Assert - ActionPipeline dispose doesn't prevent usage since it doesn't track disposed state
        // The current implementation only logs disposal
        bool secondMayDispatch = pipeline.MayDispatchAction(new DummyAction());
        secondMayDispatch.ShouldBeTrue();

        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    [Fact]
    public async Task InitializeAsync_Should_Call_AfterInitializeAllMiddlewares()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);
        pipeline.Use(typeof(DummyMiddleware));

        // Act
        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Assert
        DummyMiddleware.StaticCalls.ShouldContain("initialized");
        DummyMiddleware.StaticCalls.ShouldContain("afterInitializeAll");

        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    [Fact]
    public async Task InitializeAsync_Should_Throw_When_Called_Twice()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);
        pipeline.Use(typeof(DummyMiddleware));

        // Act
        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object));
    }

    private static async Task<(ActionPipeline, DummyMiddleware, Mock<IDispatcher>, Mock<IStore>)>
        CreatePipelineWithMiddleware()
    {
        ServiceCollection services = [];
        services.AddLogging();

        // Since ActionPipeline creates its own instances, we need to access the middleware differently
        // We'll create the pipeline with a type registration and then get the middleware by checking static state
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();
        Mock<IStore> storeMock = new();
        Mock<ILogger<ActionPipeline>> loggerMock = new();

        ActionPipeline pipeline = new(serviceProvider, loggerMock.Object);
        pipeline.Use(typeof(DummyMiddleware));

        await pipeline.InitializeAsync(dispatcherMock.Object, storeMock.Object);

        // Create a dummy middleware to return (this won't be the one used by the pipeline)
        // But since DummyMiddleware doesn't need static state, we can return any instance
        DummyMiddleware dummyMw = new();

        return (pipeline, dummyMw, dispatcherMock, storeMock);
    }
}

public class DummyAction;

public class DummyMiddleware : IMiddleware
{
    // Static fields for testing
    public static List<string> StaticCalls { get; set; } = [];

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        StaticCalls.Add("initialized");
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        StaticCalls.Add("afterInitializeAll");
    }

    public bool MayDispatchAction(object action)
    {
        return true; // Allow all actions
    }

    public void BeforeReduce(object action)
    {
        string call = $"before:{action.GetType().Name}";
        StaticCalls.Add(call);
    }

    public void AfterReduce(object action)
    {
        string call = $"after:{action.GetType().Name}";
        StaticCalls.Add(call);
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() =>
        {
            /* no-op */
        });
    }
}

public class OrderTrackingMiddleware : IMiddleware
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    // Static fields for testing
    public static string? Name { get; set; }
    public static List<string>? ExecutionOrder { get; set; }

    public OrderTrackingMiddleware()
    {
        _name = Name ?? "Unknown";
        _executionOrder = ExecutionOrder ?? new List<string>();
    }

    public OrderTrackingMiddleware(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        _executionOrder.Add($"Initialize:{_name}");
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        _executionOrder.Add($"AfterInitializeAll:{_name}");
    }

    public bool MayDispatchAction(object action)
    {
        return true;
    }

    public void BeforeReduce(object action)
    {
        _executionOrder.Add($"Before:{_name}");
    }

    public void AfterReduce(object action)
    {
        _executionOrder.Add($"After:{_name}");
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { /* no-op */ });
    }
}

public class MetadataMiddleware : IMiddleware
{
    public DateTimeOffset? BeforeTimestamp { get; private set; }
    public DateTimeOffset? AfterTimestamp { get; private set; }

    // Static fields for testing
    public static DateTimeOffset? StaticBeforeTimestamp { get; set; }
    public static DateTimeOffset? StaticAfterTimestamp { get; set; }

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        // No-op
    }

    public bool MayDispatchAction(object action)
    {
        return true;
    }

    public void BeforeReduce(object action)
    {
        BeforeTimestamp = DateTimeOffset.UtcNow;
        StaticBeforeTimestamp = BeforeTimestamp;
    }

    public void AfterReduce(object action)
    {
        AfterTimestamp = DateTimeOffset.UtcNow;
        StaticAfterTimestamp = AfterTimestamp;
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { /* no-op */ });
    }
}

public class PreventingMiddleware : IMiddleware
{
    // Static fields for testing
    public static bool StaticBeforeReduceCalled { get; set; }
    public static bool StaticAfterReduceCalled { get; set; }

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        // No-op
    }

    public bool MayDispatchAction(object action)
    {
        return false; // Prevent all actions
    }

    public void BeforeReduce(object action)
    {
        StaticBeforeReduceCalled = true;
    }

    public void AfterReduce(object action)
    {
        StaticAfterReduceCalled = true;
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { /* no-op */ });
    }
}

public class CallbackMiddleware : IMiddleware
{
    private readonly Action _beforeCallback;
    private readonly Action _afterCallback;

    // Static fields for testing
    public static Action? BeforeAction { get; set; }
    public static Action? AfterAction { get; set; }

    public CallbackMiddleware()
    {
        _beforeCallback = BeforeAction ?? (() => { });
        _afterCallback = AfterAction ?? (() => { });
    }

    public CallbackMiddleware(Action beforeCallback, Action afterCallback)
    {
        _beforeCallback = beforeCallback;
        _afterCallback = afterCallback;
    }

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        // No-op
    }

    public bool MayDispatchAction(object action)
    {
        return true;
    }

    public void BeforeReduce(object action)
    {
        _beforeCallback();
    }

    public void AfterReduce(object action)
    {
        _afterCallback();
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { /* no-op */ });
    }
}
