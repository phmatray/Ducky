// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ducky.Pipeline;

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
    public void Dispose_Should_Complete_And_Dispose_Subject()
    {
        (ActionPipeline pipeline, DummyMiddleware _, Subject<object> subject) = CreatePipelineWithMiddleware();
        var completed = false;
        using IDisposable sub = pipeline.BeforeReducePipeline.Subscribe(_ => { }, _ => completed = true);
        pipeline.Dispose();
        completed.ShouldBeTrue();
    }

    private static (ActionPipeline, DummyMiddleware, Subject<object>) CreatePipelineWithMiddleware()
    {
        Mock<IDispatcher> dispatcherMock = new();
        Subject<object> subject = new();
        dispatcherMock.Setup(d => d.ActionStream).Returns(subject.AsObservable());
        ServiceCollection services = [];
        DummyMiddleware dummyMw = new();
        services.AddSingleton(dummyMw);
        services.AddSingleton<IActionMiddleware, DummyMiddleware>();
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
