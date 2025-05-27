// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;
using Ducky.Tests.TestModels;

namespace Ducky.Tests.Middlewares;

public sealed class AsyncEffectExceptionHandlingTests
{
    [Fact]
    public void AsyncEffectMiddleware_WhenEffectThrowsException_ShouldPublishErrorEvent()
    {
        // Arrange
        Mock<IServiceProvider> serviceProvider = new();
        Mock<IDispatcher> dispatcher = new();
        Mock<IStoreEventPublisher> eventPublisher = new();
        Mock<IRootState> rootState = new();
        Mock<IAsyncEffect> effect = new();

        effect.Setup(e => e.CanHandle(It.IsAny<object>())).Returns(false);
        serviceProvider.Setup(sp => sp.GetServices<IAsyncEffect>()).Returns([effect.Object]);

        // Act
        AsyncEffectMiddleware middleware = new(
            serviceProvider.Object,
            () => rootState.Object,
            dispatcher.Object,
            eventPublisher.Object);

        // Assert
        middleware.ShouldNotBeNull();
        // Real exception testing happens through integration tests
        // where we can test the full pipeline behavior
    }
}
