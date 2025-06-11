using Ducky.Builder;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Builder;

public class StoreBuilderTests
{
    [Fact]
    public void StoreBuilder_ShouldRegisterMiddlewareCorrectly()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDuckyStore(builder =>
        {
            builder
                .AddCorrelationIdMiddleware()
                .AddAsyncEffectMiddleware();
        });

        // Assert - Middlewares should be registered as both concrete and interface
        services.Any(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(AsyncEffectMiddleware)).ShouldBeTrue();

        // Check interface registrations
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(3);
    }

    [Fact]
    public void StoreBuilder_ShouldPreventDuplicateMiddlewareRegistration()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDuckyStore(builder =>
        {
            builder
                .AddCorrelationIdMiddleware()
                .AddCorrelationIdMiddleware() // Duplicate
                .AddCorrelationIdMiddleware(); // Another duplicate
        });

        // Assert - Should only register once
        services.Count(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBe(1);
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(1);
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterCustomMiddleware()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDuckyStore(builder => builder.AddMiddleware<TestMiddleware>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterMiddlewareWithFactory()
    {
        // Arrange
        ServiceCollection services = [];
        TestMiddleware? capturedMiddleware = null;

        // Act
        services.AddDuckyStore(builder =>
        {
            builder.AddMiddleware<TestMiddleware>(_ =>
            {
                capturedMiddleware = new TestMiddleware();
                return capturedMiddleware;
            });
        });

        // Build provider and resolve
        ServiceProvider provider = services.BuildServiceProvider();
        TestMiddleware resolved = provider.GetRequiredService<TestMiddleware>();
        IMiddleware resolvedInterface = provider.GetRequiredService<IMiddleware>();

        // Assert
        resolved.ShouldBe(capturedMiddleware);
        resolvedInterface.ShouldBe(capturedMiddleware);
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterSlices()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDuckyStore(builder => builder.AddSlice<TestState>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(ISlice<TestState>)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterEffects()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDuckyStore(builder =>
        {
            builder
                .AddAsyncEffectMiddleware()
                .AddEffect<TestAsyncEffect>();
        });

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestAsyncEffect)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IAsyncEffect)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterExceptionHandlers()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDuckyStore(builder => builder.AddExceptionHandler<TestExceptionHandler>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestExceptionHandler)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IExceptionHandler)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldConfigureOptions()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDuckyStore(builder =>
        {
            builder.ConfigureOptions(options =>
            {
                // Set a property that exists on DuckyOptions
                options.AssemblyNames = ["TestAssembly"];
            });
        });

        // Assert - The configuration action should be registered in the service collection
        Action<DuckyOptions>? configAction = services.BuildServiceProvider().GetService<Action<DuckyOptions>>();
        configAction.ShouldNotBeNull();

        // Verify the action works
        DuckyOptions testOptions = new();
        configAction(testOptions);
        testOptions.AssemblyNames.ShouldBe(["TestAssembly"]);
    }

    [Fact]
    public void StoreBuilder_ShouldSupportFluentChaining()
    {
        // Arrange
        ServiceCollection services = [];

        // Act & Assert - Should compile and work
        services.AddDuckyStore(builder =>
        {
            builder
                .AddDefaultMiddlewares()
                .AddSlice<TestState>()
                .AddEffect<TestAsyncEffect>()
                .AddExceptionHandler<TestExceptionHandler>()
                .ConfigureStore(options => options.AssemblyNames = ["TestAssembly"]);
        });

        // Verify all registrations
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(3); // Default middlewares
        services.Any(sd => sd.ServiceType == typeof(ISlice<TestState>)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IAsyncEffect)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IExceptionHandler)).ShouldBeTrue();
    }

    // Test doubles
    private class TestMiddleware : MiddlewareBase;

    private class TestState : IState;

    private class TestAsyncEffect : IAsyncEffect
    {
        public object? LastAction { get; private set; }

        public void SetDispatcher(IDispatcher dispatcher)
        {
        }

        public bool CanHandle(object action) => true;

        public Task HandleAsync(object action, IRootState rootState)
        {
            LastAction = action;
            return Task.CompletedTask;
        }
    }

    private class TestExceptionHandler : IExceptionHandler
    {
        public bool HandleActionError(ActionErrorEventArgs eventArgs) => true;

        public bool HandleEffectError(EffectErrorEventArgs eventArgs) => true;
    }
}
