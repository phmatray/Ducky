using Microsoft.Extensions.Logging;

namespace Ducky.Tests.Diagnostics;

// A logging provider that is third-party code gone wrong (SPEC §6.3, SafeLogger): Log records the EventId, then throws
// whatever onLog returns for it; IsEnabled and BeginScope throw their configured exception, if any.
internal sealed class ThrowingLogger(Func<EventId, Exception?> onLog) : ILogger
{
    public List<int> Logged { get; } = [];

    // The one exception SafeLogger rethrows (§6.3, §10).
#pragma warning disable CA2201 // justification: the test simulates a provider failing with the runtime's fatal exception
    public static Exception Fatal() => new OutOfMemoryException();
#pragma warning restore CA2201

    public bool Enabled { get; init; } = true;

    public Exception? OnIsEnabled { get; init; }

    public Exception? OnBeginScope { get; init; }

    public IDisposable? Scope { get; init; }

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Logged.Add(eventId.Id);
        if (onLog(eventId) is { } thrown)
        {
            throw thrown;
        }
    }

    public bool IsEnabled(LogLevel logLevel) => OnIsEnabled is null ? Enabled : throw OnIsEnabled;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => OnBeginScope is null ? Scope : throw OnBeginScope;
}
