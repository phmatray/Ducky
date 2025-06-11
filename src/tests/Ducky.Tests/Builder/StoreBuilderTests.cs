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
        services.AddDucky(builder =>
        {
            builder
                .AddMiddleware<CorrelationIdMiddleware>()
                .AddMiddleware<AsyncEffectMiddleware>();
        });

        // Assert - Middlewares should be registered as both concrete and interface
        services.Any(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(AsyncEffectMiddleware)).ShouldBeTrue();

        // Check interface registrations
        List<ServiceDescriptor> middlewareServices = services.Where(sd => sd.ServiceType == typeof(IMiddleware)).ToList();
        // If this fails, uncomment the next line to see what middlewares are registered
        // var middlewareTypes = middlewareServices.Select(sd => sd.ImplementationType?.Name ?? sd.ImplementationFactory?.ToString() ?? "Unknown").ToList();
        middlewareServices.Count.ShouldBe(2);
    }

    [Fact]
    public void StoreBuilder_ShouldPreventDuplicateMiddlewareRegistration()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder =>
        {
            builder
                .AddMiddleware<CorrelationIdMiddleware>()
                .AddMiddleware<CorrelationIdMiddleware>() // Duplicate
                .AddMiddleware<CorrelationIdMiddleware>(); // Another duplicate
        });

        // Assert - Should only register once
        services.Count(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBe(1);
        // Default middlewares (CorrelationId + AsyncEffect) are added automatically
        // Since we're trying to add CorrelationId again, it won't add a duplicate
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(2); // CorrelationId + AsyncEffect from defaults
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterCustomMiddleware()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDucky(builder => builder.AddMiddleware<TestMiddleware>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterSlices()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDucky(builder => builder.AddSlice<TestState>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(ISlice<TestState>)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldRegisterEffects()
    {
        // Arrange
        ServiceCollection services = [];

        // Act
        services.AddDucky(builder =>
        {
            builder
                .AddMiddleware<AsyncEffectMiddleware>()
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
        services.AddDucky(builder => builder.AddExceptionHandler<TestExceptionHandler>());

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestExceptionHandler)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(IExceptionHandler)).ShouldBeTrue();
    }

    [Fact]
    public void StoreBuilder_ShouldSupportFluentChaining()
    {
        // Arrange
        ServiceCollection services = [];

        // Act & Assert - Should compile and work
        services.AddDucky(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddSlice<TestState>()
                .AddEffect<TestAsyncEffect>()
                .AddExceptionHandler<TestExceptionHandler>()
                .ConfigureStore(options => options.AssemblyNames = ["TestAssembly"]);
        });

        // Verify all registrations
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(2); // Default middlewares
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
