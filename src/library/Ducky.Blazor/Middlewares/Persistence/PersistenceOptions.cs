namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Configuration options for the persistence middleware.
/// </summary>
public class PersistenceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether persistence is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the storage key for persisted state.
    /// If null, a default key based on the state type will be used.
    /// </summary>
    public string? StorageKey { get; set; }

    /// <summary>
    /// Gets or sets the version of the persisted state schema.
    /// Used for migration purposes when state structure changes.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the throttle delay in milliseconds for persistence operations.
    /// This prevents excessive save operations during rapid state changes.
    /// </summary>
    public int ThrottleDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the debounce delay in milliseconds for persistence operations.
    /// This ensures only the final state in a series of rapid changes is saved.
    /// </summary>
    public int DebounceDelayMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the action types that should not trigger state persistence.
    /// Actions matching these types will be ignored for persistence purposes.
    /// </summary>
    public string[] ExcludedActionTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the state keys that should be persisted.
    /// If null or empty, all state will be persisted.
    /// </summary>
    public string[]? WhitelistedStateKeys { get; set; }

    /// <summary>
    /// Gets or sets the state keys that should not be persisted.
    /// These keys will be excluded from the persisted state.
    /// </summary>
    public string[] BlacklistedStateKeys { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to automatically hydrate on initialization.
    /// </summary>
    public bool AutoHydrate { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to queue actions during hydration.
    /// If true, actions dispatched during hydration will be queued and replayed after hydration completes.
    /// </summary>
    public bool QueueActionsOnHydration { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom predicate to determine if an action should trigger persistence.
    /// If null, the default logic using ExcludedActionTypes will be used.
    /// </summary>
    public Func<object, bool>? ShouldPersistAction { get; set; }

    /// <summary>
    /// Gets or sets a custom predicate to determine if the current state should be persisted.
    /// Useful for conditional persistence based on state content.
    /// </summary>
    public Func<IRootState, bool>? ShouldPersistState { get; set; }

    /// <summary>
    /// Gets or sets a custom state transformation function for persistence.
    /// Allows modifying the state before it's persisted (e.g., removing sensitive data).
    /// </summary>
    public Func<IRootState, IRootState>? TransformStateForPersistence { get; set; }

    /// <summary>
    /// Gets or sets a custom state transformation function for hydration.
    /// Allows modifying the persisted state during hydration (e.g., data migration).
    /// </summary>
    public Func<IRootState, IRootState>? TransformStateForHydration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to persist initial state immediately after hydration.
    /// This ensures the current state format is saved even if no actions are dispatched.
    /// </summary>
    public bool PersistInitialState { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of hydration retry attempts on failure.
    /// </summary>
    public int MaxHydrationRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between hydration retry attempts in milliseconds.
    /// </summary>
    public int HydrationRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether to log persistence operations for debugging.
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets error handling behavior for persistence failures.
    /// </summary>
    public PersistenceErrorHandling ErrorHandling { get; set; } = PersistenceErrorHandling.LogAndContinue;
}

/// <summary>
/// Defines how persistence errors should be handled.
/// </summary>
public enum PersistenceErrorHandling
{
    /// <summary>
    /// Log the error and continue operation. This is the safest option.
    /// </summary>
    LogAndContinue,

    /// <summary>
    /// Log the error and disable persistence for the session.
    /// </summary>
    LogAndDisable,

    /// <summary>
    /// Throw the exception, potentially crashing the application.
    /// Only use for critical debugging scenarios.
    /// </summary>
    Throw
}
