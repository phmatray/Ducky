// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky;

internal static partial class DuckyLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Store initialized")]
    public static partial void StoreInitialized(
        this ILogger<DuckyStore> logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Slice \"{SliceKey}\" added")]
    public static partial void SliceAdded(
        this ILogger<DuckyStore> logger,
        string sliceKey);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Effect [{EffectKey}] from Assembly [{AssemblyQualifiedName}] added")]
    public static partial void EffectAdded(
        this ILogger<DuckyStore> logger,
        string effectKey,
        string assemblyQualifiedName);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Disposing store")]
    public static partial void DisposingStore(
        this ILogger<DuckyStore> logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "State change observation completed")]
    public static partial void StateChangeObservationCompleted(
        this ILogger logger);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "State change observation error")]
    public static partial void StateChangeObservationError(
        this ILogger logger);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message =
            """
            [{ActionName}] action triggered at {Timestamp} took {Duration} ms
              type       → {SliceType}
              prev state → {PrevState}
              action     → {ActionName} {Action}
              next state → {NextState}
            """)]
    public static partial void LogStateChange(
        this ILogger logger,
        string actionName,
        string timestamp,
        string duration,
        string sliceType,
        string prevState,
        string action,
        string nextState);
}
