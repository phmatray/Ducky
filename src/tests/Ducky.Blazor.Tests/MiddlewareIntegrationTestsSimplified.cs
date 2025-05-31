using Bunit;
using Ducky.Builder;
using Ducky.Blazor.Builder;
using Ducky.Blazor.Middlewares.DevTools;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Diagnostics;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Shouldly;
using R3;

namespace Ducky.Blazor.Tests;

/// <summary>
/// Simplified but comprehensive middleware integration tests that focus on the core functionality.
/// </summary>
public class MiddlewareIntegrationTestsSimplified : Bunit.TestContext
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock;
    
    public MiddlewareIntegrationTestsSimplified()
    {
        _jsRuntimeMock = new Mock<IJSRuntime>();
        Services.AddSingleton(_jsRuntimeMock.Object);
        Services.AddLogging();
    }

    [Fact]
    public void StoreBuilder_WithProductionPreset_ShouldConfigureAllMiddlewareCorrectly()
    {
        // Arrange & Act
        Services.AddDuckyStore(builder => builder
            .UseProductionPreset()
            .AddDevToolsMiddleware(options => options.Enabled = true)
        );

        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        
        // Verify core services are registered
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
        
        IDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        dispatcher.ShouldNotBeNull();
        
        // Verify middleware registrations
        IEnumerable<IMiddleware> middlewares = scope.ServiceProvider.GetServices<IMiddleware>();
        middlewares.ShouldNotBeEmpty();
        middlewares.Count().ShouldBeGreaterThanOrEqualTo(4); // At least the 4 production middlewares
        
        // Verify specific middleware types are registered
        middlewares.ShouldContain(m => m.GetType() == typeof(CorrelationIdMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(ExceptionHandlingMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(AsyncEffectMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(ReactiveEffectMiddleware));
    }

    [Fact]
    public void MiddlewareOrderValidation_ShouldDetectOrderViolations()
    {
        // Act & Assert - Adding middlewares in wrong order should throw
        Should.Throw<MiddlewareOrderException>(() =>
        {
            Services.AddDuckyStore(builder => builder
                .AddAsyncEffectMiddleware()     // Should come after CorrelationId
                .AddCorrelationIdMiddleware()   // Should come first
                .AddExceptionHandlingMiddleware() // Should come after CorrelationId but before AsyncEffect
            );
            
            Services.BuildServiceProvider();
        });
    }

    [Fact]
    public void MiddlewareOrderValidation_ShouldProvideDetailedErrorMessages()
    {
        // Arrange & Act
        MiddlewareOrderException exception = Should.Throw<MiddlewareOrderException>(() =>
        {
            Services.AddDuckyStore(builder => builder
                .AddAsyncEffectMiddleware()
                .AddCorrelationIdMiddleware()
            );
            Services.BuildServiceProvider();
        });

        // Assert
        exception.Message.ShouldContain("Middleware order violations detected");
        exception.Message.ShouldContain("Suggested order:");
        exception.Violations.ShouldNotBeEmpty();
        
        MiddlewareOrderViolation violation = exception.Violations.First();
        violation.ViolationType.ShouldBe(OrderViolationType.ShouldComeBefore);
        violation.MiddlewareType.ShouldBe(typeof(CorrelationIdMiddleware));
        violation.RelatedType.ShouldBe(typeof(AsyncEffectMiddleware));
    }

    [Fact]
    public void MiddlewareOrderValidation_CanBeDisabled()
    {
        // Arrange & Act - Should not throw when validation is disabled
        Services.AddDuckyStore(builder =>
        {
            // Cast to concrete type to access DisableOrderValidation
            if (builder is StoreBuilder storeBuilder)
            {
                storeBuilder.DisableOrderValidation();
            }
            
            return builder
                .AddAsyncEffectMiddleware()     // Invalid order, but validation disabled
                .AddCorrelationIdMiddleware()
                .AddExceptionHandlingMiddleware();
        });

        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
    }

    [Fact]
    public void StoreBuilderPresets_ShouldConfigureCorrectMiddlewares()
    {
        // Test Production Preset
        TestPreset(
            builder => builder.UseProductionPreset(), 
            expectedMiddlewareCount: 4);

        // Test Development Preset  
        TestPreset(
            builder => builder.UseDevelopmentPreset(),
            expectedMiddlewareCount: 4); // Same as production

        // Test Testing Preset
        TestPreset(
            builder => builder.UseTestingPreset(),
            expectedMiddlewareCount: 2); // Just ExceptionHandling + AsyncEffect
    }

    private void TestPreset(Func<IStoreBuilder, IStoreBuilder> configurePreset, int expectedMiddlewareCount)
    {
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock.Object);
        tempServices.AddLogging();
        
        tempServices.AddDuckyStore(builder => configurePreset(builder));
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();

        using IServiceScope scope = serviceProvider.CreateScope();
        IEnumerable<IMiddleware> middlewares = scope.ServiceProvider.GetServices<IMiddleware>();
        
        middlewares.Count().ShouldBeGreaterThanOrEqualTo(expectedMiddlewareCount);
        
        // All presets should have at least CorrelationId and ExceptionHandling
        middlewares.ShouldContain(m => m.GetType() == typeof(CorrelationIdMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(ExceptionHandlingMiddleware));
    }

    [Fact]
    public void UseDevelopmentPreset_ShouldConfigureCorrectly()
    {
        // Arrange
        Services.AddDuckyStore(builder => builder
            .UseDevelopmentPreset()
        );
        
        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Act - Just creating the store should register middleware
        using IServiceScope scope = serviceProvider.CreateScope();
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        
        // Assert - verify development preset includes all production middleware
        IEnumerable<IMiddleware> middlewares = scope.ServiceProvider.GetServices<IMiddleware>();
        middlewares.ShouldContain(m => m.GetType() == typeof(CorrelationIdMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(ExceptionHandlingMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(AsyncEffectMiddleware));
        middlewares.ShouldContain(m => m.GetType() == typeof(ReactiveEffectMiddleware));
    }

    [Fact]
    public void BlazorMiddlewares_ShouldRegisterCorrectly()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<object>("console.log", It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<object>(null!));

        // Act
        Services.AddDuckyStore(builder => builder
            .UseProductionPreset()
            .AddJsLoggingMiddleware()
            .AddDevToolsMiddleware(options => 
            {
                options.StoreName = "TestStore";
                options.Enabled = true;
            })
        );
        
        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        
        // Verify Blazor-specific services are registered
        scope.ServiceProvider.GetService<JsConsoleLoggerModule>().ShouldNotBeNull();
        scope.ServiceProvider.GetService<ReduxDevToolsModule>().ShouldNotBeNull();
        
        DevToolsOptions devToolsOptions = scope.ServiceProvider.GetRequiredService<DevToolsOptions>();
        devToolsOptions.StoreName.ShouldBe("TestStore");
        devToolsOptions.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void MissingMiddlewareException_ShouldProvideActionableErrorMessages()
    {
        // Act & Assert
        MissingMiddlewareException exception = Should.Throw<MissingMiddlewareException>(() =>
        {
            Services.AddDuckyStore(builder => builder
                .AddCorrelationIdMiddleware()
                .AddExceptionHandlingMiddleware()
                // Missing AsyncEffectMiddleware but adding an effect
                .AddEffect<TestAsyncEffect>()
            );
            Services.BuildServiceProvider();
        });

        // Assert
        exception.Message.ShouldContain("Cannot add TestAsyncEffect without AsyncEffectMiddleware");
        exception.Message.ShouldContain("AddAsyncEffectMiddleware()");
        exception.Message.ShouldContain("Example:");
        exception.EffectType.ShouldBe(typeof(TestAsyncEffect));
        exception.RequiredMiddlewareType.ShouldBe(typeof(AsyncEffectMiddleware));
    }

    [Fact]
    public void AllMiddlewarePresets_ShouldWorkTogether()
    {
        // Arrange & Act - Test all presets work
        Services.AddDuckyStore(builder => builder
            .UseAllMiddlewares()
        );
        
        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
        
        IEnumerable<IMiddleware> middlewares = scope.ServiceProvider.GetServices<IMiddleware>();
        middlewares.Count().ShouldBeGreaterThan(4); // Should have all middlewares
    }

    [Fact]
    public void CustomExceptionHandler_ShouldBeRegistered()
    {
        // Arrange & Act
        Services.AddDuckyStore(builder => builder
            .UseProductionPreset()
            .AddExceptionHandler<TestExceptionHandler>()
        );
        
        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        IEnumerable<IExceptionHandler> exceptionHandlers = scope.ServiceProvider.GetServices<IExceptionHandler>();
        exceptionHandlers.ShouldContain(h => h.GetType() == typeof(TestExceptionHandler));
    }
}

#region Test Support Types

public class TestAsyncEffect : AsyncEffect<TestAction>
{
    public override Task HandleAsync(TestAction action, IRootState rootState)
    {
        return Task.CompletedTask;
    }
}

public class TestExceptionHandler : IExceptionHandler
{
    public bool HandleActionError(ActionErrorEventArgs eventArgs) => true;
    public bool HandleEffectError(EffectErrorEventArgs eventArgs) => true;
}

public record TestAction(string Message);

#endregion