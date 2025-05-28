// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;

namespace Ducky.Tests.Integration;

public class MiddlewareRegistrationTests
{
    [Fact]
    public void Middleware_WhenRegisteredWithExtensionMethod_ShouldBeRegisteredInDI()
    {
        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act - Register middlewares using their extension methods
        services.AddCorrelationIdMiddleware();
        services.AddExceptionHandlingMiddleware();
        services.AddAsyncEffectMiddleware();
        services.AddReactiveEffectMiddleware();

        // Assert - Middlewares should be registered in the service collection
        services.Any(sd => sd.ServiceType == typeof(CorrelationIdMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ExceptionHandlingMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(AsyncEffectMiddleware)).ShouldBeTrue();
        services.Any(sd => sd.ServiceType == typeof(ReactiveEffectMiddleware)).ShouldBeTrue();

        // Also check that they are registered as IActionMiddleware
        services.Where(sd => sd.ServiceType == typeof(IActionMiddleware)).Count().ShouldBe(4);
    }

    [Fact]
    public void Middleware_RegistrationMethods_ShouldRegisterBothConcreteAndInterface()
    {
        // This test verifies that the original middleware registration issue is fixed
        // where middlewares were not being registered as both concrete type and IActionMiddleware

        // Arrange
        ServiceCollection services = [];
        services.AddLogging();

        // Act
        services.AddCorrelationIdMiddleware();

        // Assert - Both registrations should exist
        ServiceDescriptor? concreteRegistration =
            services.FirstOrDefault(sd => sd.ServiceType == typeof(CorrelationIdMiddleware));
        ServiceDescriptor? interfaceRegistration = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IActionMiddleware));

        concreteRegistration.ShouldNotBeNull();
        interfaceRegistration.ShouldNotBeNull();
    }
}
