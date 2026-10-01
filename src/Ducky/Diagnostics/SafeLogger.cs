using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky;

// SPEC §6.3: a logging provider is third-party code, so no Ducky log call may throw. Every ILogger Ducky logs through is
// wrapped once, where it is obtained; only OutOfMemoryException (fatal, §10) gets through.
#pragma warning disable CA1031 // justification: every non-fatal provider failure is swallowed by design (§6.3, INV-03)
internal sealed class SafeLogger(ILogger inner) : ILogger
{
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        try
        {
            inner.Log(logLevel, eventId, state, exception, formatter);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        try
        {
            return inner.IsEnabled(logLevel);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        try
        {
            return inner.BeginScope(state);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return NullLogger.Instance.BeginScope(state);
        }
    }
}
#pragma warning restore CA1031
