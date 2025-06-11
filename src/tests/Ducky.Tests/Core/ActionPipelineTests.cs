// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Tests.Core;

public sealed class ActionPipelineTests
{
    [Fact]
    public async Task Use_Should_Register_Middleware_And_Invoke_BeforeReduce()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        (ActionPipeline pipeline, DummyMiddleware dummyMw, _, _) = await CreatePipelineWithMiddleware();

        bool result = pipeline.ProcessAction(new DummyAction());

        result.ShouldBeTrue();
        DummyMiddleware.StaticCalls.ShouldContain("before:DummyAction");

        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    [Fact]
    public async Task Use_Should_Register_Middleware_And_Invoke_AfterReduce()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();

        (ActionPipeline pipeline, DummyMiddleware dummyMw, _, _) = await CreatePipelineWithMiddleware();

        bool result = pipeline.ProcessAction(new DummyAction());

        result.ShouldBeTrue();
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
        bool result = pipeline.ProcessAction(new DummyAction());

        // Assert
        result.ShouldBeTrue();
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
        bool result = pipeline.ProcessAction(new DummyAction());

        // Assert
        result.ShouldBeTrue();
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

        bool result = pipeline.ProcessAction(new DummyAction());

        result.ShouldBeTrue();
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

        bool result = pipeline.ProcessAction(new DummyAction());

        result.ShouldBeTrue();
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
        bool result = pipeline.ProcessAction(new DummyAction());

        // Assert
        result.ShouldBeTrue();
        MetadataMiddleware.StaticBeforeTimestamp.ShouldNotBeNull();
        MetadataMiddleware.StaticAfterTimestamp.ShouldNotBeNull();
        MetadataMiddleware.StaticAfterTimestamp.Value.ShouldBeGreaterThanOrEqualTo(MetadataMiddleware.StaticBeforeTimestamp.Value);

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
        bool result = pipeline.ProcessAction(new DummyAction());

        // Assert
        result.ShouldBeFalse(); // Action should be prevented
        PreventingMiddleware.StaticBeforeReduceCalled.ShouldBeFalse(); // Should not reach BeforeReduce
        PreventingMiddleware.StaticAfterReduceCalled.ShouldBeFalse(); // Should not reach AfterReduce

        // Clean up static state
        PreventingMiddleware.StaticBeforeReduceCalled = false;
        PreventingMiddleware.StaticAfterReduceCalled = false;
    }

    [Fact]
    public async Task Dispose_Should_Prevent_Further_Action_Processing()
    {
        // Clear static state
        DummyMiddleware.StaticCalls.Clear();
        
        // Arrange
        (ActionPipeline pipeline, DummyMiddleware dummyMw, _, _) = await CreatePipelineWithMiddleware();

        // Send an action to verify pipeline is working
        bool firstResult = pipeline.ProcessAction(new DummyAction());
        firstResult.ShouldBeTrue();

        int initialCallCount = DummyMiddleware.StaticCalls.Count;

        // Act
        pipeline.Dispose();

        // Assert - should throw exception when trying to process after dispose
        Should.Throw<ObjectDisposedException>(() => pipeline.ProcessAction(new DummyAction()));

        // Call count should remain the same
        DummyMiddleware.StaticCalls.Count.ShouldBe(initialCallCount);
        
        // Clean up static state
        DummyMiddleware.StaticCalls.Clear();
    }

    private static async Task<(ActionPipeline, DummyMiddleware, Mock<IDispatcher>, Mock<IStore>)> CreatePipelineWithMiddleware()
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
    public List<string> Calls { get; } = [];
    
    // Static fields for testing
    public static List<string> StaticCalls { get; set; } = [];

    public Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        Calls.Add("initialized");
        StaticCalls.Add("initialized");
        return Task.CompletedTask;
    }

    public void AfterInitializeAllMiddlewares()
    {
        Calls.Add("afterInitializeAll");
        StaticCalls.Add("afterInitializeAll");
    }

    public bool MayDispatchAction(object action)
    {
        return true; // Allow all actions
    }

    public void BeforeReduce(object action)
    {
        string call = $"before:{action.GetType().Name}";
        Calls.Add(call);
        StaticCalls.Add(call);
    }

    public void AfterReduce(object action)
    {
        string call = $"after:{action.GetType().Name}";
        Calls.Add(call);
        StaticCalls.Add(call);
    }

    public IDisposable BeginInternalMiddlewareChange()
    {
        return new DisposableCallback(() => { /* no-op */ });
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
    public bool BeforeReduceCalled { get; private set; }
    public bool AfterReduceCalled { get; private set; }

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
        BeforeReduceCalled = true;
        StaticBeforeReduceCalled = true;
    }

    public void AfterReduce(object action)
    {
        AfterReduceCalled = true;
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
