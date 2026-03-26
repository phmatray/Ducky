// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Centralized factory for creating descriptive exceptions.
/// Each exception answers: what happened, why, and what to do.
/// </summary>
internal static class ExceptionFactory
{
    public static DuckyException SliceNotFound(Type stateType)
        => new(
            $"Slice of type '{stateType.Name}' not found. "
            + $"Did you register it with builder.AddSlice<YourReducers>()? "
            + $"Check with store.HasSlice<{stateType.Name}>().");

    public static KeyNotFoundException SliceKeyNotFound(string key)
        => new(
            $"Slice with key '{key}' not found. "
            + "Verify the slice key matches the kebab-case name derived from your reducer class name.");

    public static DuckyException StateIsNull(string sliceKey)
        => new(
            $"State for slice '{sliceKey}' is null. "
            + "Ensure the slice reducer returns a non-null initial state from GetInitialState().");

    public static InvalidOperationException ReentrantDepthExceeded(
        int maxDepth,
        string? currentAction,
        string? newAction)
        => new(
            $"Action '{newAction}' aborted: re-entrant dispatch depth exceeded maximum of {maxDepth}. "
            + $"This usually means a reducer or effect is dispatching in a loop. "
            + $"Check the '{currentAction}' -> '{newAction}' dispatch chain.");

    public static DuckyException DispatcherDisposed()
        => new(
            "Cannot dispatch to a disposed Dispatcher. Ensure components unsubscribe in Dispose().",
            new ObjectDisposedException(nameof(Dispatcher)));

    public static KeyNotFoundException EntityNotFound<TKey>(TKey key, Type entityType)
        => new(
            $"Entity with key '{key}' not found in NormalizedState"
            + $"<{typeof(TKey).Name}, {entityType.Name}>. "
            + "Call HasEntity() before accessing, or use TryGetEntity() for safe access.");
}
