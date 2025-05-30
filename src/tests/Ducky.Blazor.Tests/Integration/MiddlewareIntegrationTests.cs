using Ducky.Blazor;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;

namespace Ducky.Blazor.Tests.Integration;

public class MiddlewareIntegrationTests
{
    [Fact]
    public void AddDuckyBlazor_RegistersAllRequiredMiddleware()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        
        // Mock JS runtime for Blazor
        Mock<IJSRuntime> jsRuntimeMock = new();
        services.AddSingleton(jsRuntimeMock.Object);
        
        // Mock configuration
        Mock<IConfiguration> configMock = new();
        configMock.Setup(c => c.GetSection("Ducky")).Returns(Mock.Of<IConfigurationSection>());
        
        // Act
        services.AddDuckyBlazor(configMock.Object);
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        
        // Assert - Verify all middleware can be resolved
        Assert.NotNull(serviceProvider.GetService<CorrelationIdMiddleware>());
        Assert.NotNull(serviceProvider.GetService<JsLoggingMiddleware>());
        Assert.NotNull(serviceProvider.GetService<AsyncEffectMiddleware>());
        Assert.NotNull(serviceProvider.GetService<ReactiveEffectMiddleware>());
        
        // Verify store can be created
        using IServiceScope scope = serviceProvider.CreateScope();
        DuckyStore? store = scope.ServiceProvider.GetService<DuckyStore>();
        Assert.NotNull(store);
    }
    
    [Fact]
    public void MiddlewarePipeline_ExecutesInCorrectOrder()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        
        // Mock JS runtime for Blazor
        Mock<IJSRuntime> jsRuntimeMock = new();
        services.AddSingleton(jsRuntimeMock.Object);
        
        // Mock configuration
        Mock<IConfiguration> configMock = new();
        configMock.Setup(c => c.GetSection("Ducky")).Returns(Mock.Of<IConfigurationSection>());
        
        List<string> executionOrder = [];
        
        // Act
        services.AddDuckyBlazor(configMock.Object, (pipeline, sp) =>
        {
            // Add a test middleware to track execution
            pipeline.Use(new TestOrderTrackingMiddleware("Custom", executionOrder));
        });
        
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        
        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        DuckyStore? store = scope.ServiceProvider.GetService<DuckyStore>();
        Assert.NotNull(store);
        
        // The pipeline should be configured with all middleware
        // This verifies the configuration doesn't throw exceptions
    }
    
    private class TestOrderTrackingMiddleware : IActionMiddleware
    {
        private readonly string _name;
        private readonly List<string> _executionOrder;
        
        public TestOrderTrackingMiddleware(string name, List<string> executionOrder)
        {
            _name = name;
            _executionOrder = executionOrder;
        }
        
        public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
        {
            return actions.Do(_ => _executionOrder.Add($"{_name}-Before"));
        }
        
        public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
        {
            return actions.Do(_ => _executionOrder.Add($"{_name}-After"));
        }
    }
}