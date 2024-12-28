// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky;

/// <summary>
/// A fallback logger that writes to the console if no other logger is available.
/// </summary>
public sealed class ConsoleFallbackLogger<T> : ILogger<T>
{
    private static readonly NullScope Scope = new();

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel)
        => true;

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return Scope;
    }

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string>? formatter)
    {
        if (!IsEnabled(logLevel) || formatter is null)
        {
            return;
        }

        string message = formatter(state, exception);
        Console.WriteLine($"{DateTimeOffset.Now:u} [{typeof(T).Name}] {logLevel}: {message}");

        if (exception is null)
        {
            return;
        }

        Console.WriteLine(exception);
    }

    private class NullScope : IDisposable
    {
        public void Dispose()
        {
            // no-op
        }
    }
}
