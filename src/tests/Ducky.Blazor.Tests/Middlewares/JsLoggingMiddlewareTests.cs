using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Pipeline;
using Ducky.Tests.TestModels;
using Microsoft.JSInterop;
using Moq;
using R3;

namespace Ducky.Blazor.Tests.Middlewares;

public class JsLoggingMiddlewareTests
{
    [Fact]
    public void InvokeBeforeReduce_AddsMetadataToContext()
    {
        // Arrange
        Mock<IJSRuntime> jsRuntimeMock = new();
        JsConsoleLoggerModule loggerModule = new(jsRuntimeMock.Object);
        TestState testState = new() { Value = 42 };
        IRootState rootState = Factories.CreateTestRootState();
        JsLoggingMiddleware middleware = new(loggerModule, () => rootState);
        
        TestAction action = new();
        ActionContext context = new(action);
        Observable<ActionContext> actions = Observable.Return(context);
        
        // Act
        Observable<ActionContext> result = middleware.InvokeBeforeReduce(actions);
        ActionContext? processedContext = null;
        result.Subscribe(ctx => processedContext = ctx);
        
        // Assert
        Assert.NotNull(processedContext);
        Assert.True(processedContext.Metadata.ContainsKey("PrevState"));
        Assert.True(processedContext.Metadata.ContainsKey("StartTime"));
    }
    
    [Fact]
    public async Task InvokeAfterReduce_LogsToJavaScriptConsole()
    {
        // Arrange
        Mock<IJSRuntime> jsRuntimeMock = new();
        JsConsoleLoggerModule loggerModule = new(jsRuntimeMock.Object);
        TestState testState = new() { Value = 42 };
        IRootState rootState = Factories.CreateTestRootState();
        JsLoggingMiddleware middleware = new(loggerModule, () => rootState);
        
        TestAction action = new();
        ActionContext context = new(action);
        context.SetMetadata("PrevState", rootState);
        context.SetMetadata("StartTime", DateTime.Now);
        Observable<ActionContext> actions = Observable.Return(context);
        
        // Act
        Observable<ActionContext> result = middleware.InvokeAfterReduce(actions);
        result.Subscribe();
        
        // Allow async operations to complete
        await Task.Delay(100);
        
        // Assert
        jsRuntimeMock.Verify(js => js.InvokeAsync<object>(
            It.Is<string>(s => s.Contains("jsConsoleLogger.log")),
            It.IsAny<object?[]>()), 
            Times.Once);
    }
}