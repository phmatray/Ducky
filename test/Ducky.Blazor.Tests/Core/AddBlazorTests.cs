using Ducky.Blazor.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Blazor.Tests.Core;

// SPEC §11.1 (AddBlazor), §5.1 (IsBrowser), §6.10 (store identity); INV-22.
public sealed class AddBlazorTests
{
    private static readonly ServiceProviderOptions _validating = new() { ValidateScopes = true, ValidateOnBuild = true };

    [Fact]
    public void AddBlazor_ConfigureDelegatesCompose()
    {
        // Every delegate of every call runs, in call order, on one BlazorOptions: the last value written wins.
        var services = new ServiceCollection();
        var seen = new List<string>();
        services.AddDucky(d => d
            .AddBlazor(o =>
            {
                seen.Add($"first:{o.KeyPrefix}:{o.IsBrowser}");
                o.KeyPrefix = "app";
                o.IsBrowser = true;
            })
            .AddBlazor()
            .AddBlazor(o =>
            {
                seen.Add($"third:{o.KeyPrefix}:{o.IsBrowser}");
                o.KeyPrefix = "shop";
            }));

        using var provider = services.BuildServiceProvider(_validating);
        var options = provider.GetRequiredService<BlazorOptions>();

        seen.ShouldBe(["first:ducky:False", "third:app:True"]);
        options.KeyPrefix.ShouldBe("shop");
        options.IsBrowser.ShouldBeTrue();
        services.Count(descriptor => descriptor.ServiceType == typeof(BlazorOptions)).ShouldBe(1);
        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.AddBlazor(null!));
    }

    [Fact]
    public void AddBlazor_UserRegisteredOptions_StillRegistersOwn()
    {
        // A BlazorOptions the app registered itself does not count as a first AddBlazor call.
        var services = new ServiceCollection();
        var user = new BlazorOptions { KeyPrefix = "user" };
        services.AddSingleton(user);
        services.AddDucky(d => d.AddBlazor(o => o.KeyPrefix = "app").AddBlazor(o => o.KeyPrefix += "!"));

        using var provider = services.BuildServiceProvider(_validating);
        var options = provider.GetRequiredService<BlazorOptions>();

        options.ShouldNotBeSameAs(user);
        options.KeyPrefix.ShouldBe("app!");
        user.KeyPrefix.ShouldBe("user");
    }

    [Fact]
    public async Task DuplicateRegistration_AddSliceTwice_OneSlice()
    {
        // The 1.x regression: a slice (or AddBlazor) registered twice must reduce each action once, in one slice.
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddBlazor().AddSlice<CounterSlice>().AddSlice<CounterSlice>().AddBlazor());

        await using var provider = services.BuildServiceProvider(_validating);
        await using var scope = provider.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();

        store.Slices.Select(static slice => slice.GetType()).ShouldBe([typeof(PersistenceSlice), typeof(CounterSlice)]);
        services.Count(descriptor => descriptor.ServiceType == typeof(CounterSlice)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(PersistenceSlice)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(BlazorOptions)).ShouldBe(1);
        (await store.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Counter>().Value.ShouldBe(1);
    }

    [Fact]
    public async Task NoStaticState_TwoCircuitsIsolated()
    {
        // Two circuits are two DI scopes of one server app: each has its own store and its own interop bridge, and
        // nothing one does is visible to the other (no static store, slice, module or options cache).
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddBlazor().AddSlice<CounterSlice>());

        await using var provider = services.BuildServiceProvider(_validating);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var store = first.ServiceProvider.GetRequiredService<IStore>();
        var other = second.ServiceProvider.GetRequiredService<IStore>();

        other.ShouldNotBeSameAs(store);
        (await store.DispatchAsync(new Increment())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Counter>().Value.ShouldBe(1);
        other.State.Get<Counter>().Value.ShouldBe(0);

        var firstJs = new FakeJsRuntime();
        var secondJs = new FakeJsRuntime();
        await using var firstBridge = new JsBridge(firstJs, NullLogger.Instance);
        await using var secondBridge = new JsBridge(secondJs, NullLogger.Instance);
        (await firstBridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeTrue();
        (await firstBridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeTrue();
        (await secondBridge.TryInvokeAsync<object>("ready", TestContext.Current.CancellationToken)).Delivered.ShouldBeTrue();

        firstJs.Calls.Select(call => call.Identifier).ShouldBe(["import", "ready", "ready"]);
        secondJs.Calls.Select(call => call.Identifier).ShouldBe(["import", "ready"]);
    }
}
