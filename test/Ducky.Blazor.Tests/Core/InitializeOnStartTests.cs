using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ducky.Blazor.Tests.Core;

// §11.1 InitializeOnStart: the hosted service that starts init when a WebAssembly host starts (.NET 11 runs it after the
// page's state was restored).
public sealed class InitializeOnStartTests
{
    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    public async Task StartAsync_InitializesOnlyInTheBrowserAndNotOptedOut(bool browser, bool initializeOnStart, int expected)
    {
        var inits = new InitCounter();
        var services = new ServiceCollection();
        services.AddSingleton(inits);
        services.AddDucky(d => d.Use<InitTap>().AddBlazor(o => (o.IsBrowser, o.InitializeOnStart) = (browser, initializeOnStart)));
        await using var provider = services.BuildServiceProvider();

        foreach (var service in provider.GetServices<IHostedService>())
        {
            await service.StartAsync(CancellationToken.None);
        }

        // The start fires init without awaiting it: wait for the one idempotent run to land.
        if (expected > 0)
        {
            await provider.GetRequiredService<IStore>().InitializeAsync(TestContext.Current.CancellationToken);
        }

        inits.Count.ShouldBe(expected);
        await provider.GetServices<IHostedService>().Single().StopAsync(CancellationToken.None);
    }

    [Fact]
    public void AddBlazor_RegistersOneHostedServiceHoweverOftenCalled()
    {
        var services = new ServiceCollection();
        services.AddDucky(d => d.AddBlazor().AddBlazor());

        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)).ShouldBe(1);
    }

    private sealed class InitCounter
    {
        public int Count;
    }

    private sealed class InitTap(InitCounter counter) : Middleware
    {
        public override ValueTask InitializeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref counter.Count);
            return default;
        }
    }
}
