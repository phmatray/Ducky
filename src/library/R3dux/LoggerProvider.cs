// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Provides a centralized mechanism to configure and obtain logger instances.
/// </summary>
public static class LoggerProvider
{
    private static ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Configures the global logger factory used to create logger instances.
    /// </summary>
    /// <param name="loggerFactory">The logger factory to use for creating logger instances.</param>
    public static void Configure(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a logger instance for the specified type.
    /// </summary>
    /// <typeparam name="T">The type for which a logger is to be created.</typeparam>
    /// <returns>A logger instance for the specified type.</returns>
    public static ILogger<T>? CreateLogger<T>()
    {
        return _loggerFactory?.CreateLogger<T>();
    }
}
