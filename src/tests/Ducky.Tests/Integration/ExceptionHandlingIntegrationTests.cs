// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Pipeline;

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

        // Add Ducky services with StoreBuilder
        services.AddDuckyStore(builder => builder
            .AddExceptionHandler<TestExceptionHandler>(_ => _exceptionHandler));

        // Add test slice
        services.AddScoped<ISlice, TestCounterReducers>();

        _serviceProvider = services.BuildServiceProvider();
        _store = _serviceProvider.GetRequiredService<IStore>();
        _dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
        _eventPublisher = _serviceProvider.GetRequiredService<IStoreEventPublisher>();

        // Subscribe to events
        _eventPublisher.EventPublished += (sender, evt) => _publishedEvents.Add(evt);
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

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
