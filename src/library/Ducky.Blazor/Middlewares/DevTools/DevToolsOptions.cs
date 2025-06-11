using System.Text.RegularExpressions;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Configuration options for Redux DevTools integration.
/// </summary>
public class DevToolsOptions
{
    /// <summary>
    /// Gets or sets the name of the store as displayed in DevTools.
    /// </summary>
    public string StoreName { get; set; } = "DuckyStore";

    /// <summary>
    /// Gets or sets a value indicating whether DevTools should be enabled.
    /// Typically disabled in production environments.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether real-time updates should be sent to DevTools.
    /// </summary>
    public bool Realtime { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stack traces should be included.
    /// </summary>
    public bool Trace { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of stack trace items to include.
    /// </summary>
    public int TraceLimit { get; set; } = 25;

    /// <summary>
    /// Gets or sets a value indicating whether time-travel debugging should be enabled.
    /// When enabled, DevTools can send state changes back to the application.
    /// </summary>
    public bool EnableTimeTravel { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of actions to keep in DevTools history.
    /// </summary>
    public int MaxAge { get; set; } = 50;

    /// <summary>
    /// Gets or sets action types that should be excluded from DevTools logging.
    /// Useful for filtering out noisy actions like timer ticks.
    /// </summary>
    public string[] ExcludedActionTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether actions should be collapsed by default in DevTools.
    /// </summary>
    public bool CollapseActions { get; set; } = false;

    /// <summary>
    /// Gets or sets a custom predicate to determine if an action should be sent to DevTools.
    /// If null, all actions (except excluded types) will be sent.
    /// </summary>
    public Func<object, bool>? ShouldLogAction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stack traces should be captured and sent to DevTools.
    /// </summary>
    public bool StackTraceEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of stack frames to capture.
    /// 0 means unlimited.
    /// </summary>
    public int StackTraceLimit { get; set; } = 10;

    /// <summary>
    /// Gets or sets a regex pattern to filter stack trace frames.
    /// Only frames matching this pattern will be included.
    /// Default excludes System, Microsoft, and Ducky internals.
    /// </summary>
    public Regex? StackTraceFilterRegex { get; set; } = new(
        @"^(?!.*(System|Microsoft|Ducky\.Pipeline|Ducky\.Abstractions)).*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Gets or sets the latency in milliseconds for batching DevTools updates.
    /// </summary>
    public int Latency { get; set; } = 500;

    /// <summary>
    /// Gets or sets custom action sanitizer function.
    /// Can be used to remove sensitive data from actions before sending to DevTools.
    /// </summary>
    public Func<object, object>? ActionSanitizer { get; set; }

    /// <summary>
    /// Gets or sets custom state sanitizer function.
    /// Can be used to remove sensitive data from state before sending to DevTools.
    /// </summary>
    public Func<object, object>? StateSanitizer { get; set; }
}
