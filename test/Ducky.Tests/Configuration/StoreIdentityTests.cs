using Ducky.Tests.BuilderFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.10 (DI registration and store identity), §5.1 (Lifetime, TimeProvider); INV-22, ADR-0025.
public sealed class StoreIdentityTests
{
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };

    [Fact]
    public async Task BrowserLifetime_RootAndScopes_ResolveOneStore()
    {
        var services = new ServiceCollection().AddScoped<ScopedDependency>().AddLogging(logging => logging.AddFakeLogging());
        services.AddDucky(d =>
        {
            d.IsBrowser = true;
            d.AddSlice<CartSlice>();
        });

        await using var provider = services.BuildServiceProvider(_validating);
        await using var renderer = provider.CreateAsyncScope();
        await using var handler = provider.CreateAsyncScope();
        var store = provider.GetRequiredService<IStore>();

        // Root, renderer scope and handler scope: one store, one dispatcher, one store-owned slice instance.
        renderer.ServiceProvider.GetRequiredService<IStore>().ShouldBeSameAs(store);
        handler.ServiceProvider.GetRequiredService<IStore>().ShouldBeSameAs(store);
        provider.GetRequiredService<IDispatcher>().ShouldBeSameAs(store);
        handler.ServiceProvider.GetRequiredService<IDispatcher>().ShouldBeSameAs(store);
        renderer.ServiceProvider.GetRequiredService<CartSlice>().ShouldBeSameAs(store.Slices.Single());
        (await handler.ServiceProvider.GetRequiredService<IDispatcher>().DispatchAsync(new AddItem())).ShouldBe(DispatchResult.Reduced);
        renderer.ServiceProvider.GetRequiredService<IStore>().State.Get<Cart>().Items.ShouldBe(1);

        // The store-owned scope resolves scoped dependencies legally under scope validation, as its own instances.
        var storeScope = ((DuckyStore)store).Scope.ShouldNotBeNull().ServiceProvider;
        var dependency = storeScope.GetRequiredService<ScopedDependency>();
        storeScope.GetRequiredService<ScopedDependency>().ShouldBeSameAs(dependency);
        renderer.ServiceProvider.GetRequiredService<ScopedDependency>().ShouldNotBeSameAs(dependency);
        provider.GetRequiredService<FakeLogCollector>().GetSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task ServerLifetime_TwoScopes_TwoStores()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddSlice<CartSlice>().AddSlice<OrderSlice>());

        await using var provider = services.BuildServiceProvider(_validating);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var store = first.ServiceProvider.GetRequiredService<IStore>();
        var other = second.ServiceProvider.GetRequiredService<IStore>();

        other.ShouldNotBeSameAs(store);
        first.ServiceProvider.GetRequiredService<IStore>().ShouldBeSameAs(store);
        first.ServiceProvider.GetRequiredService<IDispatcher>().ShouldBeSameAs(store);
        second.ServiceProvider.GetRequiredService<IDispatcher>().ShouldBeSameAs(other);
        first.ServiceProvider.GetRequiredService<CartSlice>().ShouldBeSameAs(store.Slices[0]);
        first.ServiceProvider.GetRequiredService<OrderSlice>().ShouldBeSameAs(store.Slices[1]);
        second.ServiceProvider.GetRequiredService<CartSlice>().ShouldBeSameAs(other.Slices[0]);

        (await first.ServiceProvider.GetRequiredService<IDispatcher>().DispatchAsync(new AddItem())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Cart>().Items.ShouldBe(1);
        other.State.Get<Cart>().Items.ShouldBe(0);

        // A Scoped store resolves its effects and middleware from its own DI scope: it owns no scope.
        ((DuckyStore)store).Scope.ShouldBeNull();
    }

    // §6.10, §23: code outside the circuit scope (here a handler built in its own scope, as IHttpClientFactory does) gets
    // a different, empty store, and what it dispatches never reaches the circuit's store.
    [Fact]
    public async Task ServerLifetime_HandlerScope_GetsDistinctStore()
    {
        var services = new ServiceCollection().AddSingleton<HandlerFactory>().AddTransient<StoreReachingHandler>();
        services.AddDucky(d => d.AddSlice<CartSlice>());

        await using var provider = services.BuildServiceProvider(_validating);
        await using var circuit = provider.CreateAsyncScope();
        var circuitStore = circuit.ServiceProvider.GetRequiredService<IStore>();
        (await circuitStore.DispatchAsync(new AddItem())).ShouldBe(DispatchResult.Reduced);
        await using var handlerScope = circuit.ServiceProvider.GetRequiredService<HandlerFactory>().CreateHandler(out var handler);

        handler.Store.ShouldNotBeSameAs(circuitStore);
        handler.Store.State.Get<Cart>().Items.ShouldBe(0);
        (await handler.Dispatcher.DispatchAsync(new AddItem())).ShouldBe(DispatchResult.Reduced);
        handler.Store.State.Get<Cart>().Items.ShouldBe(1);
        circuitStore.State.Get<Cart>().Items.ShouldBe(1);
    }

    // §5.1: the unset Lifetime is resolved after configure returns, so IsBrowser set last still decides it; IStore,
    // IDispatcher and every slice share it. A Singleton outside the browser logs Warning 1004; Transient is DUCKY301.
    [Theory]
    [InlineData(false, null, ServiceLifetime.Scoped)]
    [InlineData(true, null, ServiceLifetime.Singleton)]
    [InlineData(false, ServiceLifetime.Scoped, ServiceLifetime.Scoped)]
    [InlineData(true, ServiceLifetime.Scoped, ServiceLifetime.Scoped)]
    [InlineData(false, ServiceLifetime.Singleton, ServiceLifetime.Singleton)]
    [InlineData(true, ServiceLifetime.Singleton, ServiceLifetime.Singleton)]
    [InlineData(false, ServiceLifetime.Transient, ServiceLifetime.Transient)]
    [InlineData(true, ServiceLifetime.Transient, ServiceLifetime.Transient)]
    public async Task Lifetime_Default_FollowsIsBrowserSetInConfigure(bool isBrowser, ServiceLifetime? lifetime, ServiceLifetime expected)
    {
        var services = new ServiceCollection().AddLogging(logging => logging.AddFakeLogging());
        services.AddDucky(d =>
        {
            d.AddSlice<CartSlice>();
            if (lifetime is { } explicitLifetime)
            {
                d.Lifetime = explicitLifetime;
            }

            d.IsBrowser = isBrowser;
        });

        services.Where(s => s.ServiceType == typeof(IStore) || s.ServiceType == typeof(IDispatcher) || s.ServiceType == typeof(CartSlice))
            .Select(s => s.Lifetime).ShouldBe([expected, expected, expected]);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var logs = provider.GetRequiredService<FakeLogCollector>();
        if (expected == ServiceLifetime.Transient)
        {
            Should.Throw<DuckyConfigurationException>(() => scope.ServiceProvider.GetRequiredService<IStore>())
                .Errors.ShouldBe([DuckyErrors.TransientLifetime()]);
            logs.GetSnapshot().ShouldBeEmpty();
            return;
        }

        scope.ServiceProvider.GetRequiredService<IStore>().ShouldNotBeNull();
        if (expected == ServiceLifetime.Singleton && !isBrowser)
        {
            var warning = logs.GetSnapshot().ShouldHaveSingleItem();
            warning.Id.Id.ShouldBe(1004);
            warning.Level.ShouldBe(LogLevel.Warning);
        }
        else
        {
            logs.GetSnapshot().ShouldBeEmpty();
        }
    }

    // Non-normative (§5.1): AddDucky adds TimeProvider.System only when the app registered none.
    [Fact]
    public void AddDucky_TimeProvider_TryAddsSystem()
    {
        var plain = new ServiceCollection().AddDucky(_ => { });
        var fake = new FixedTimeProvider();
        var preset = new ServiceCollection().AddSingleton<TimeProvider>(fake).AddDucky(_ => { });

        using var plainProvider = plain.BuildServiceProvider();
        using var presetProvider = preset.BuildServiceProvider();

        plainProvider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
        preset.Count(s => s.ServiceType == typeof(TimeProvider)).ShouldBe(1);
        presetProvider.GetRequiredService<TimeProvider>().ShouldBeSameAs(fake);
    }

    private sealed class ScopedDependency;

    private sealed class FixedTimeProvider : TimeProvider;

    private sealed class StoreReachingHandler(IStore store, IDispatcher dispatcher)
    {
        public IStore Store => store;

        public IDispatcher Dispatcher => dispatcher;
    }

    // Builds each handler in a scope of its own, created from the root, as IHttpClientFactory does.
    private sealed class HandlerFactory(IServiceScopeFactory scopes)
    {
        public AsyncServiceScope CreateHandler(out StoreReachingHandler handler)
        {
            var scope = scopes.CreateAsyncScope();
            handler = scope.ServiceProvider.GetRequiredService<StoreReachingHandler>();
            return scope;
        }
    }
}
