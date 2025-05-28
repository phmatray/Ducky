// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Tests.Core;

public sealed class ActionPipelineTests
{
    [Fact]
    public void Use_Should_Register_Middleware_And_Invoke_BeforeReduce()
    {
        (ActionPipeline pipeline, DummyMiddleware dummyMw, Subject<object> subject) = CreatePipelineWithMiddleware();
        List<ActionContext> received = [];
        using IDisposable sub = pipeline.SubscribeBefore(new SliceObserver(received.Add));
        subject.OnNext(new DummyAction());
        received.Count.ShouldBe(1);
        dummyMw.Calls.ShouldContain("before:DummyAction");
    }

    [Fact]
    public void Use_Should_Register_Middleware_And_Invoke_AfterReduce()
    {
        (ActionPipeline pipeline, DummyMiddleware dummyMw, Subject<object> subject) = CreatePipelineWithMiddleware();
        List<ActionContext> received = [];
        using IDisposable sub = pipeline.SubscribeAfter(new SliceObserver(received.Add));
        subject.OnNext(new DummyAction());
        received.Count.ShouldBe(1);
        dummyMw.Calls.ShouldContain("after:DummyAction");
    }

    [Fact]
    public void Middlewares_Should_Execute_In_Correct_Order()
    {
        // Arrange
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());

        ActionPipeline pipeline = new(dispatcherMock.Object);
        List<string> executionOrder = [];

        // Register outer middleware
        OrderTrackingMiddleware outerMiddleware = new("Outer", executionOrder);
        pipeline.Use(outerMiddleware);

        // Register inner middleware
        OrderTrackingMiddleware innerMiddleware = new("Inner", executionOrder);
        pipeline.Use(innerMiddleware);

        // Act - Subscribe to reduce notification first to ensure it's set up
        using IDisposable reduceSub = pipeline.ReduceNotification.Subscribe(_ => executionOrder.Add("Reduce"));

        // Then subscribe to before and after pipelines
        List<ActionContext> beforeReceived = [];
        List<ActionContext> afterReceived = [];
        using IDisposable beforeSub = pipeline.SubscribeBefore(new SliceObserver(beforeReceived.Add));
        using IDisposable afterSub = pipeline.SubscribeAfter(new SliceObserver(afterReceived.Add));

        subject.OnNext(new DummyAction());

        // Assert - Verify execution order
        executionOrder.ShouldBe(new[]
        {
            "Before:Outer",
            "Before:Inner",
            "Reduce",
            "After:Inner",
            "After:Outer"
        });
    }

    [Fact]
    public void Multiple_Middlewares_Should_Maintain_Nesting_Order()
    {
        // Arrange
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());

        ActionPipeline pipeline = new(dispatcherMock.Object);
        List<string> executionOrder = [];

        // Register multiple middlewares
        for (int i = 1; i <= 3; i++)
        {
            pipeline.Use(new OrderTrackingMiddleware($"MW{i}", executionOrder));
        }

        // Act - Subscribe to reduce notification first
        using IDisposable reduceSub = pipeline.ReduceNotification.Subscribe(_ => executionOrder.Add("Reduce"));

        List<ActionContext> beforeReceived = [];
        List<ActionContext> afterReceived = [];
        using IDisposable beforeSub = pipeline.SubscribeBefore(new SliceObserver(beforeReceived.Add));
        using IDisposable afterSub = pipeline.SubscribeAfter(new SliceObserver(afterReceived.Add));

        subject.OnNext(new DummyAction());

        // Assert
        executionOrder.ShouldBe(new[]
        {
            "Before:MW1",
            "Before:MW2",
            "Before:MW3",
            "Reduce",
            "After:MW3",
            "After:MW2",
            "After:MW1"
        });
    }

    [Fact]
    public void UseBefore_Should_Invoke_Custom_Before_Middleware()
    {
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());
        ActionPipeline pipeline = new(dispatcherMock.Object);
        var called = false;
        pipeline.UseBefore(action =>
        {
            called = true;
            return Observable.Return(new ActionContext(action));
        });
        List<ActionContext> received = [];
        using IDisposable sub = pipeline.SubscribeBefore(new SliceObserver(received.Add));
        subject.OnNext(new DummyAction());
        called.ShouldBeTrue();
        received.Count.ShouldBe(1);
    }

    [Fact]
    public void UseAfter_Should_Invoke_Custom_After_Middleware()
    {
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());
        ActionPipeline pipeline = new(dispatcherMock.Object);
        var called = false;
        pipeline.UseAfter(action =>
        {
            called = true;
            return Observable.Return(new ActionContext(action));
        });
        List<ActionContext> received = [];
        using IDisposable sub = pipeline.SubscribeAfter(new SliceObserver(received.Add));
        subject.OnNext(new DummyAction());
        called.ShouldBeTrue();
        received.Count.ShouldBe(1);
    }

    [Fact]
    public void Middleware_Should_Be_Able_To_Modify_ActionContext()
    {
        // Arrange
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());

        ActionPipeline pipeline = new(dispatcherMock.Object);

        // Middleware that adds metadata
        MetadataMiddleware metadataMiddleware = new();
        pipeline.Use(metadataMiddleware);

        // Act
        List<ActionContext> beforeReceived = [];
        List<ActionContext> afterReceived = [];
        using IDisposable beforeSub = pipeline.SubscribeBefore(new SliceObserver(beforeReceived.Add));
        using IDisposable afterSub = pipeline.SubscribeAfter(new SliceObserver(afterReceived.Add));

        subject.OnNext(new DummyAction());

        // Assert
        beforeReceived[0].Metadata.ShouldContainKey("BeforeTimestamp");
        afterReceived[0].Metadata.ShouldContainKey("AfterTimestamp");
        afterReceived[0].Metadata.ShouldContainKey("BeforeTimestamp");
    }

    [Fact]
    public void Aborted_Actions_Should_Not_Reach_After_Pipeline()
    {
        // Arrange
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());

        ActionPipeline pipeline = new(dispatcherMock.Object);

        // Middleware that aborts
        AbortingMiddleware abortingMiddleware = new();
        pipeline.Use(abortingMiddleware);

        // Act
        List<ActionContext> beforeReceived = [];
        List<ActionContext> afterReceived = [];
        using IDisposable beforeSub = pipeline.SubscribeBefore(new SliceObserver(beforeReceived.Add));
        using IDisposable afterSub = pipeline.SubscribeAfter(new SliceObserver(afterReceived.Add));

        subject.OnNext(new DummyAction());

        // Assert
        beforeReceived.Count.ShouldBe(1);
        beforeReceived[0].IsAborted.ShouldBeTrue();
        afterReceived.Count.ShouldBe(0); // Should not reach after pipeline
    }

    [Fact]
    public void Dispose_Should_Complete_And_Dispose_Subjects()
    {
        // Arrange
        (ActionPipeline pipeline, DummyMiddleware _, Subject<object> subject) = CreatePipelineWithMiddleware();
        List<ActionContext> beforeReceived = [];
        List<ActionContext> afterReceived = [];

        // Subscribe to check that streams are active
        using IDisposable beforeSub = pipeline.SubscribeBefore(new SliceObserver(beforeReceived.Add));
        using IDisposable afterSub = pipeline.SubscribeAfter(new SliceObserver(afterReceived.Add));

        // Send an action to verify pipeline is working
        subject.OnNext(new DummyAction());
        beforeReceived.Count.ShouldBe(1);
        afterReceived.Count.ShouldBe(1);

        // Act
        pipeline.Dispose();

        // Assert - no more actions should be processed after dispose
        subject.OnNext(new DummyAction());
        beforeReceived.Count.ShouldBe(1); // Should still be 1
        afterReceived.Count.ShouldBe(1); // Should still be 1
    }

    private static (ActionPipeline, DummyMiddleware, Subject<object>) CreatePipelineWithMiddleware()
    {
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());
        ServiceCollection services = [];
        DummyMiddleware dummyMw = new();
        services.TryAddScoped(_ => dummyMw);
        services.TryAddScoped<IActionMiddleware, DummyMiddleware>();
        ActionPipeline pipeline = new(dispatcherMock.Object);
        pipeline.Use(dummyMw);
        return (pipeline, dummyMw, subject);
    }
}

public class DummyAction;

public class DummyMiddleware : IActionMiddleware
{
    public List<string> Calls { get; } = [];

    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
    {
        return src.Do(ctx => Calls.Add($"before:{ctx.Action.GetType().Name}"));
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
    {
        return src.Do(ctx => Calls.Add($"after:{ctx.Action.GetType().Name}"));
    }
}

public class OrderTrackingMiddleware : IActionMiddleware
{
    private readonly string _name;
    private readonly List<string> _executionOrder;

    public OrderTrackingMiddleware(string name, List<string> executionOrder)
    {
        _name = name;
        _executionOrder = executionOrder;
    }

    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
    {
        return src.Do(_ => _executionOrder.Add($"Before:{_name}"));
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
    {
        return src.Do(_ => _executionOrder.Add($"After:{_name}"));
    }
}

public class MetadataMiddleware : IActionMiddleware
{
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
    {
        return src.Do(ctx => ctx.Metadata["BeforeTimestamp"] = DateTimeOffset.UtcNow);
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
    {
        return src.Do(ctx => ctx.Metadata["AfterTimestamp"] = DateTimeOffset.UtcNow);
    }
}

public class AbortingMiddleware : IActionMiddleware
{
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> src)
    {
        return src.Do(ctx => ctx.Abort());
    }

    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> src)
    {
        return src; // Should never be called if aborted
    }
}
