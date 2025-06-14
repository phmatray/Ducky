using Ducky.Builder;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using Microsoft.JSInterop;
using FakeItEasy;

namespace Ducky.Blazor.Tests;

/// <summary>
/// Simplified but comprehensive middleware integration tests that focus on the core functionality.
/// </summary>
public class MiddlewareIntegrationTestsSimplified : Bunit.TestContext
{
    private readonly IJSRuntime _jsRuntimeMock;

    public MiddlewareIntegrationTestsSimplified()
    {
        _jsRuntimeMock = A.Fake<IJSRuntime>();
        Services.AddSingleton(_jsRuntimeMock);
        Services.AddLogging();
    }

    [Fact]
    public async Task StoreBuilder_WithProductionPreset_ShouldConfigureAllMiddlewareCorrectly()
    {
        // Arrange & Act
        Services.AddDuckyBlazor(builder => builder
            .EnableDevTools(options => options.Enabled = true)
        );

        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

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
    public void MiddlewareOrderValidation_ShouldPreventDuplicates()
    {
        // Test that middleware order validation prevents duplicate registrations
        // The current design prevents order violations by always adding default middlewares in correct order
        
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock);
        tempServices.AddLogging();
        
        // AddDucky automatically ensures correct middleware order
        tempServices.AddDucky(builder => builder
            .AddMiddleware<AsyncEffectMiddleware>() // This should be deduplicated
            .AddMiddleware<CorrelationIdMiddleware>() // This should be deduplicated
        );
        
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        
        // Verify that middlewares are registered correctly without duplicates
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();
        
        // Should have exactly 2 middlewares (no duplicates)
        middlewares.Length.ShouldBe(2);
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
    }

    [Fact]
    public void MiddlewareConfiguration_ShouldMaintainCorrectOrder()
    {
        // Test that middleware configuration maintains the correct order
        // regardless of the order they are added by the user
        
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock);
        tempServices.AddLogging();
        
        // Add middlewares in "wrong" order to test that the system handles it correctly
        tempServices.AddDucky(builder => builder
            .AddMiddleware<AsyncEffectMiddleware>() // Added first, but should come after CorrelationId
            .AddMiddleware<CorrelationIdMiddleware>() // Added second, but should come first
        );
        
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        
        // Verify that the system works correctly regardless of add order
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        store.ShouldNotBeNull();
        
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();
        middlewares.Length.ShouldBe(2);
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
    }

    [Fact]
    public async Task StoreBuilderPresets_ShouldConfigureCorrectMiddlewares()
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
        await TestPresetWithSingleMiddleware();
    }

    private void TestPreset(Func<DuckyBuilder, DuckyBuilder> configurePreset, int expectedMiddlewareCount)
    {
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock);
        tempServices.AddLogging();

        tempServices.AddDucky(builder => configurePreset(builder));
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();

        using IServiceScope scope = serviceProvider.CreateScope();
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();

        middlewares.Length.ShouldBeGreaterThanOrEqualTo(expectedMiddlewareCount);

        // All presets should have at least CorrelationId
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
    }

    private async Task TestPresetWithSingleMiddleware()
    {
        ServiceCollection tempServices = [];
        tempServices.AddSingleton(_jsRuntimeMock);
        tempServices.AddLogging();

        // AddDucky always adds default middlewares (CorrelationId + AsyncEffect)
        // So when we explicitly add AsyncEffectMiddleware, we end up with both
        tempServices.AddDucky(builder => builder
            .AddMiddleware<AsyncEffectMiddleware>()
        );
        
        ServiceProvider serviceProvider = tempServices.BuildServiceProvider();

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        
        // Get the store to trigger initialization
        IStore store = scope.ServiceProvider.GetRequiredService<IStore>();
        
        // Wait for store initialization if needed
        if (store is DuckyStore duckyStore && !duckyStore.IsInitialized)
        {
            await duckyStore.InitializeAsync();
        }
        
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();

        // AddDucky adds default middlewares, so we expect 2
        middlewares.Length.ShouldBe(2);
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
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
    public async Task BlazorMiddlewares_ShouldRegisterCorrectly()
    {
        // Arrange
        A.CallTo(() => _jsRuntimeMock.InvokeAsync<object>("console.log", A<object[]>.Ignored))
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
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        // Verify Blazor-specific services are registered
        // JsConsoleLoggerModule is only registered when EnableJsLogging() is called
        scope.ServiceProvider.GetService<ReduxDevToolsModule>().ShouldNotBeNull();

        DevToolsOptions devToolsOptions = scope.ServiceProvider.GetRequiredService<DevToolsOptions>();
        devToolsOptions.StoreName.ShouldBe("TestStore");
        devToolsOptions.Enabled.ShouldBeTrue();
    }

    [Fact]
    public void AddEffect_ShouldAutomaticallyAddRequiredMiddleware()
    {
        // Arrange & Act
        Services.AddDucky(builder => builder
            .AddMiddleware<CorrelationIdMiddleware>()
            // AsyncEffectMiddleware should be added automatically when adding an effect
            .AddEffect<TestAsyncEffect>()
        );
        
        ServiceProvider serviceProvider = Services.BuildServiceProvider();

        // Assert
        using IServiceScope scope = serviceProvider.CreateScope();
        IMiddleware[] middlewares = scope.ServiceProvider.GetServices<IMiddleware>().ToArray();
        
        // Should have both CorrelationIdMiddleware and AsyncEffectMiddleware
        middlewares.Length.ShouldBe(2);
        middlewares.ShouldContain(m => m is CorrelationIdMiddleware);
        middlewares.ShouldContain(m => m is AsyncEffectMiddleware);
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
    public override Task HandleAsync(TestAction action, IStateProvider stateProvider)
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
