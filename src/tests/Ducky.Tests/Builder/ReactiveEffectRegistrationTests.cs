// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.CorrelationId;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Tests.Builder;

public class ReactiveEffectRegistrationTests
{
    [Fact]
    public void AddReactiveEffect_ShouldRegisterBothConcreteTypeAndBaseClass()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder =>
        {
            builder.AddReactiveEffect<TestReactiveEffect>();
        });

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestReactiveEffect)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffect)).ShouldBeTrue();
    }

    [Fact]
    public void AddReactiveEffect_ShouldAutoAddReactiveEffectMiddleware()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder =>
        {
            builder
                .AddMiddleware<CorrelationIdMiddleware>()
                .AddReactiveEffect<TestReactiveEffect>();
        });

        // Assert
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();
    }

    [Fact]
    public void AddReactiveEffects_WithConfigure_ShouldRegisterEffects()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder =>
        {
            builder.AddReactiveEffects(fx =>
            {
                fx.Add<TestReactiveEffect>();
            });
        });

        // Assert
        services.Any(sd => sd.ServiceType == typeof(TestReactiveEffect)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffect)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();
    }

    [Fact]
    public void AddReactiveEffect_ShouldResolveAtRuntime()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();
        services.AddDucky(builder =>
        {
            builder.AddReactiveEffect<TestReactiveEffect>();
        });

        // Act
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IEnumerable<ReactiveEffect> effects = scope.ServiceProvider.GetServices<ReactiveEffect>();

        // Assert
        List<ReactiveEffect> effectList = effects.ToList();
        effectList.ShouldNotBeEmpty();
        effectList.ShouldContain(e => e is TestReactiveEffect);
    }

    [Fact]
    public void UseDefaultMiddlewares_ShouldIncludeReactiveEffectMiddleware()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDucky(builder =>
        {
            builder.UseDefaultMiddlewares();
        });

        // Assert
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(3); // CorrelationId + AsyncEffect + ReactiveEffect
    }

    [Fact]
    public void MiddlewareOrderValidator_ShouldAcceptReactiveEffectAfterCorrelationId()
    {
        // Arrange & Act - should not throw
        ServiceCollection services = [];
        services.AddLogging();

        services.AddDucky(builder =>
        {
            builder
                .AddMiddleware<CorrelationIdMiddleware>()
                .AddReactiveEffect<TestReactiveEffect>();
        });

        // Assert - no exception thrown means order is valid
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();
    }

    [Fact]
    public void AddReactiveEffect_ShouldWorkWithFluentChaining()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act & Assert - Should compile and work with full fluent chain
        services.AddDucky(builder =>
        {
            builder
                .UseDefaultMiddlewares()
                .AddSlice<TestStateReducers>()
                .AddReactiveEffect<TestReactiveEffect>();
        });

        // Verify all registrations
        services.Count(sd => sd.ServiceType == typeof(IMiddleware)).ShouldBe(3); // Default middlewares
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffect)).ShouldBeTrue();
    }

    private class TestState : IState;

    private sealed record TestStateReducers : SliceReducers<TestState>
    {
        public override TestState GetInitialState() => new();
    }

    // Test doubles
    private class TestReactiveEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(
            IObservable<object> actions,
            IObservable<IStateProvider> stateProvider)
        {
            return Observable.Empty<object>();
        }
    }
}
