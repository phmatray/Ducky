// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

internal static partial class DuckyBlazorLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component initializing")]
    public static partial void ComponentInitializing(
        this ILogger logger, string componentName);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component initialized")]
    public static partial void ComponentInitialized(
        this ILogger logger, string componentName);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component refreshed")]
    public static partial void ComponentRefreshed(
        this ILogger logger, string componentName);
}
