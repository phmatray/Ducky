// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace R3dux;

internal static partial class StoreLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Store initialized")]
    public static partial void StoreInitialized(
        this ILogger<Store> logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Slice \"{SliceKey}\" added")]
    public static partial void SliceAdded(
        this ILogger<Store> logger,
        string sliceKey);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "Effect [{EffectKey}] from Assembly [{AssemblyQualifiedName}] added")]
    public static partial void EffectAdded(
        this ILogger<Store> logger,
        string effectKey,
        string assemblyQualifiedName);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Disposing store")]
    public static partial void DisposingStore(
        this ILogger<Store> logger);
}
