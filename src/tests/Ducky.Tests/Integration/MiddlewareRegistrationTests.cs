// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Builder;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;

namespace Ducky.Tests.Integration;

public class MiddlewareRegistrationTests
{
    [Fact]
    public void Middleware_WhenRegisteredWithStoreBuilder_ShouldBeRegisteredInDI()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act - Register middlewares using StoreBuilder
        services.AddDuckyStore(builder => builder
            .AddCorrelationIdMiddleware()
            .AddAsyncEffectMiddleware()
            .AddReactiveEffectMiddleware());

        // Assert - Middlewares should be registered in the service collection
        services.Any(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(AsyncEffectMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();

        // Also check that they are registered as IMiddleware
        services.Where(sd => sd.ServiceType == typeof(IMiddleware)).Count().ShouldBe(4);
    }

    [Fact]
    public void StoreBuilder_RegistrationMethods_ShouldRegisterBothConcreteAndInterface()
    {
        // This test verifies that StoreBuilder correctly registers middlewares
        // as both concrete type and IMiddleware

        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddDuckyStore(builder => builder
            .AddCorrelationIdMiddleware());

        // Assert - Both registrations should exist
        ServiceDescriptor? concreteRegistration =
            services.FirstOrDefault(sd => sd.ServiceType == typeof(CorrelationIdMiddleware));
        ServiceDescriptor? interfaceRegistration = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IMiddleware));

        concreteRegistration.ShouldNotBeNull();
        interfaceRegistration.ShouldNotBeNull();
    }
}
