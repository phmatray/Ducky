using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using Microsoft.JSInterop;
using Moq;
using Shouldly;

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
        Services.AddDuckyBlazor(builder => builder
            .EnableDevTools(options => options.Enabled = true)
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
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();
        middlewares.ShouldNotBeEmpty();
        middlewares.Length.ShouldBeGreaterThanOrEqualTo(2); // At least the 2 production middlewares

        // Verify specific middleware types are registered
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
    }

    [Fact]
    public void MiddlewareOrderValidation_ShouldDetectOrderViolations()
    {
        // Act & Assert - Adding middlewares in wrong order should throw
        Should.Throw<MiddlewareOrderException>(() =>
        {
            Services.AddDucky(builder => builder
                .AddMiddleware<AsyncEffectMiddleware>() // Should come after CorrelationId
                .AddMiddleware<CorrelationIdMiddleware>() // Should come first
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
            Services.AddDucky(builder => builder
                .AddMiddleware<AsyncEffectMiddleware>()
                .AddMiddleware<CorrelationIdMiddleware>()
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
    public void StoreBuilderPresets_ShouldConfigureCorrectMiddlewares()
    {
        // Test Production Preset
        TestPreset(
            builder => builder.UseDefaultMiddlewares(),
            expectedMiddlewareCount: 2);

        // Test Development Preset  
        TestPreset(
            builder => builder.UseDefaultMiddlewares(),
            expectedMiddlewareCount: 2); // Same as production

        // Test Testing Preset - Now using just one middleware
        TestPresetWithSingleMiddleware();
    }

    private void TestPreset(Func<DuckyBuilder, DuckyBuilder> configurePreset, int expectedMiddlewareCount)
    {
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock.Object);
        tempServices.AddLogging();

        tempServices.AddDucky(builder => configurePreset(builder));
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();

        using IServiceScope scope = serviceProvider.CreateScope();
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();

        middlewares.Length.ShouldBeGreaterThanOrEqualTo(expectedMiddlewareCount);

        // All presets should have at least CorrelationId
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
    }

    private void TestPresetWithSingleMiddleware()
    {
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock.Object);
        tempServices.AddLogging();

        tempServices.AddDucky(builder => builder
            .AddMiddleware<AsyncEffectMiddleware>()
        );
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();

        using IServiceScope scope = serviceProvider.CreateScope();
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();

        middlewares.Length.ShouldBe(1);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
    }

    [Fact]
    public void UseDevelopmentPreset_ShouldConfigureCorrectly()
    {
        // Arrange
        Services.AddDucky(builder => builder
            .UseDefaultMiddlewares()
        );

        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Act - Just creating the store should register middleware
        using IServiceScope scope = serviceProvider.CreateScope();
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();

        // Assert - verify development preset includes all production middleware
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
    }

    [Fact]
    public void BlazorMiddlewares_ShouldRegisterCorrectly()
    {
        // Arrange
        _jsRuntimeMock
            .Setup(js => js.InvokeAsync<object>("console.log", It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult<object>(null!));

        // Act
        Services.AddDuckyBlazor(builder => builder
            .EnableDevTools(options =>
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
            Services.AddDucky(builder => builder
                .AddMiddleware<CorrelationIdMiddleware>()
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
        Services.AddDucky(builder => builder
            .UseDefaultMiddlewares()
        );

        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();

        IEnumerable<IMiddleware> middlewares = scope.ServiceProvider.GetServices<IMiddleware>();
        middlewares.Count().ShouldBeGreaterThanOrEqualTo(2); // Should have default middlewares
    }

    [Fact]
    public void CustomExceptionHandler_ShouldBeRegistered()
    {
        // Arrange & Act
        Services.AddDucky(builder => builder
            .UseDefaultMiddlewares()
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
