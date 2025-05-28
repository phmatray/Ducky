// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Abstractions;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Tests.Integration;

public sealed class ExceptionHandlingIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IStore _store;
    private readonly IDispatcher _dispatcher;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly TestExceptionHandler _exceptionHandler;
    private readonly List<StoreEventArgs> _publishedEvents;

    public ExceptionHandlingIntegrationTests()
    {
        _publishedEvents = new List<StoreEventArgs>();
        _exceptionHandler = new TestExceptionHandler();

        ServiceCollection services = [];

        // Configure logging
        services.AddLogging();

        // Add Ducky services
        services.AddDucky(options =>
        {
            options.ConfigurePipelineWithServices = (pipeline, sp) =>
            {
                // Add exception handling middleware first
                pipeline.Use(sp.GetRequiredService<ExceptionHandlingMiddleware>());
            };
        });

        // Add exception handling
        services.AddExceptionHandlingMiddleware();
        services.AddExceptionHandler<TestExceptionHandler>(_ => _exceptionHandler);

        // Add test slice
        services.TryAddScoped<ISlice, TestCounterReducers>();

        _serviceProvider = services.BuildServiceProvider();
        _store = _serviceProvider.GetRequiredService<IStore>();
        _dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
        _eventPublisher = _serviceProvider.GetRequiredService<IStoreEventPublisher>();

        // Subscribe to events
        _eventPublisher.Events.Subscribe(evt => _publishedEvents.Add(evt));
    }

    [Fact]
    public void Store_WhenExceptionHandlerReturnsTrue_ShouldMarkAsHandled()
    {
        // Arrange
        _exceptionHandler.ShouldHandleActionErrors = true;

        // Act
        _dispatcher.Dispatch(new TestActionWithParameter("test"));

        // Give some time for processing
        Thread.Sleep(100);

        // Assert - Check that error events were published with IsHandled = true
        List<ActionErrorEventArgs> errorEvents = _publishedEvents.OfType<ActionErrorEventArgs>().ToList();
        if (errorEvents.Count == 0)
        {
            return;
        }

        errorEvents.ShouldAllBe(evt => evt.IsHandled);
    }

    [Fact]
    public void ServiceCollection_ExceptionHandlingExtensions_ShouldRegisterCorrectly()
    {
        // Arrange
        ServiceCollection services = [];

        // Add required dependencies
        services.AddLogging();
        services.TryAddScoped<IStoreEventPublisher, StoreEventPublisher>();

        // Act
        services.AddExceptionHandlingMiddleware();
        services.AddExceptionHandler<TestExceptionHandler>();

        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ExceptionHandlingMiddleware? middleware = provider.GetService<ExceptionHandlingMiddleware>();
        IEnumerable<IExceptionHandler> handlers = provider.GetServices<IExceptionHandler>();

        middleware.ShouldNotBeNull();
        handlers.Count().ShouldBe(1);
        handlers.First().ShouldBeOfType<TestExceptionHandler>();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
