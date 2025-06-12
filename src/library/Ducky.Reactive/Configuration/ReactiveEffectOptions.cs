// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive;

/// <summary>
/// Configuration options for reactive effects.
/// </summary>
public class ReactiveEffectOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether effects should automatically unsubscribe on store disposal.
    /// Default is true.
    /// </summary>
    public bool AutoUnsubscribe { get; set; } = true;

    /// <summary>
    /// Gets or sets the default error handling behavior for effects.
    /// Default is Continue.
    /// </summary>
    public ErrorHandlingBehavior DefaultErrorBehavior { get; set; } = ErrorHandlingBehavior.Continue;

    /// <summary>
    /// Gets or sets a value indicating whether to enable detailed diagnostics for effects.
    /// Default is false.
    /// </summary>
    public bool EnableDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent effects that can be active.
    /// Null means no limit. Default is null.
    /// </summary>
    public int? MaxConcurrentEffects { get; set; }

    /// <summary>
    /// Gets or sets the default timeout for effect operations.
    /// Null means no timeout. Default is null.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to log effect lifecycle events.
    /// Default is false.
    /// </summary>
    public bool LogLifecycleEvents { get; set; }
}

/// <summary>
/// Defines error handling behavior for reactive effects.
/// </summary>
public enum ErrorHandlingBehavior
{
    /// <summary>
    /// Continue processing other effects even if one fails.
    /// </summary>
    Continue,

    /// <summary>
    /// Stop all effects if any effect fails.
    /// </summary>
    StopAll,

    /// <summary>
    /// Retry the failed effect based on retry policy.
    /// </summary>
    Retry,

    /// <summary>
    /// Isolate the failed effect but continue others.
    /// </summary>
    Isolate
}
