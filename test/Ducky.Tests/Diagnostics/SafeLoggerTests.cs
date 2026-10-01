using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests.Diagnostics;

// SPEC §6.3 (SafeLogger); INV-03.
public sealed class SafeLoggerTests
{
    [Fact]
    public async Task SafeLogger_ProviderThrows_NeverEscapes()
    {
        // Every non-fatal throw is swallowed: IsEnabled then says false and BeginScope returns a no-op scope.
        var failing = new ThrowingLogger(_ => new InvalidOperationException("log"))
        {
            OnIsEnabled = new AggregateException("enabled"),
            OnBeginScope = new InvalidOperationException("scope"),
        };
        var safe = new SafeLogger(failing);
#pragma warning disable CA1848 // justification: the test calls ILogger.Log directly, as a provider sees it
        safe.LogWarning(new EventId(7), "swallowed");
#pragma warning restore CA1848
        failing.Logged.ShouldBe([7]);
        safe.IsEnabled(LogLevel.Error).ShouldBeFalse();
        safe.BeginScope("scope").ShouldNotBeNull().Dispose();

        // OutOfMemoryException is fatal (§10): it passes through every member.
        var fatal = new ThrowingLogger(_ => ThrowingLogger.Fatal())
        {
            OnIsEnabled = ThrowingLogger.Fatal(),
            OnBeginScope = ThrowingLogger.Fatal(),
        };
        var unsafeFatal = new SafeLogger(fatal);
#pragma warning disable CA1848 // justification: as above
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.LogWarning(new EventId(8), "fatal"));
#pragma warning restore CA1848
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.IsEnabled(LogLevel.Error));
        Should.Throw<OutOfMemoryException>(() => unsafeFatal.BeginScope("scope"));

        // A healthy provider sees every call unchanged.
        var fake = new FakeLogger();
        var error = new InvalidOperationException("payload");
#pragma warning disable CA1848 // justification: as above
        new SafeLogger(fake).LogError(new EventId(9), error, "forwarded {Value}", 42);
#pragma warning restore CA1848
        var record = fake.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(9);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeSameAs(error);
        record.Message.ShouldBe("forwarded 42");
        var scope = new MemoryStream();
        var healthy = new SafeLogger(new ThrowingLogger(_ => null) { Enabled = false, Scope = scope });
        healthy.IsEnabled(LogLevel.Error).ShouldBeFalse();
        new SafeLogger(new ThrowingLogger(_ => null)).IsEnabled(LogLevel.Error).ShouldBeTrue();
        healthy.BeginScope("scope").ShouldBeSameAs(scope);

        // The store wraps its logger once, at construction: the reducer's EventId 1000 log throws inside the provider
        // and nothing escapes Process, so the catch-all (EventId 1012) never runs.
        var provider = new ThrowingLogger(_ => new InvalidOperationException("provider"));
        var store = new DuckyStore([new CountSlice(), new BoomSlice()], provider);
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        provider.Logged.ShouldBe([1000]);
    }
}
