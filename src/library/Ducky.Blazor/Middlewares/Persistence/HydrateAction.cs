namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Action used to hydrate the store with persisted state.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public sealed record HydrateAction<TState>(TState State)
    where TState : class;

/// <summary>
/// Enhanced hydrate action with metadata and source information.
/// </summary>
/// <param name="State">The state to hydrate with.</param>
/// <param name="Metadata">Metadata associated with the persisted state.</param>
/// <param name="Source">The source of the hydration (e.g., "localStorage", "sessionStorage").</param>
/// <param name="HydrationId">Unique identifier for this hydration operation.</param>
/// <typeparam name="TState">The type of the state.</typeparam>
public sealed record EnhancedHydrateAction<TState>(
    TState State,
    PersistenceMetadata Metadata,
    string Source,
    string HydrationId) where TState : class;

/// <summary>
/// Action dispatched when hydration starts.
/// </summary>
/// <param name="Source">The source of the hydration.</param>
/// <param name="HydrationId">Unique identifier for this hydration operation.</param>
public sealed record HydrationStartedAction(string Source, string HydrationId);

/// <summary>
/// Action dispatched when hydration completes successfully.
/// </summary>
/// <param name="Source">The source of the hydration.</param>
/// <param name="HydrationId">Unique identifier for this hydration operation.</param>
/// <param name="StateRestored">Whether any state was actually restored.</param>
/// <param name="Duration">The duration of the hydration operation.</param>
public sealed record HydrationCompletedAction(
    string Source,
    string HydrationId,
    bool StateRestored,
    TimeSpan Duration);

/// <summary>
/// Action dispatched when hydration fails.
/// </summary>
/// <param name="Source">The source of the hydration.</param>
/// <param name="HydrationId">Unique identifier for this hydration operation.</param>
/// <param name="Error">The error that occurred during hydration.</param>
/// <param name="Duration">The duration before the hydration failed.</param>
public sealed record HydrationFailedAction(
    string Source,
    string HydrationId,
    string Error,
    TimeSpan Duration);

/// <summary>
/// Action dispatched when persistence is triggered.
/// </summary>
/// <param name="Trigger">What triggered the persistence (e.g., "action", "timer").</param>
/// <param name="PersistenceId">Unique identifier for this persistence operation.</param>
public sealed record PersistenceTriggeredAction(string Trigger, string PersistenceId);

/// <summary>
/// Action dispatched when persistence completes successfully.
/// </summary>
/// <param name="PersistenceId">Unique identifier for this persistence operation.</param>
/// <param name="BytesSaved">Number of bytes saved.</param>
/// <param name="Duration">The duration of the persistence operation.</param>
public sealed record PersistenceCompletedAction(
    string PersistenceId,
    long BytesSaved,
    TimeSpan Duration);

/// <summary>
/// Action dispatched when persistence fails.
/// </summary>
/// <param name="PersistenceId">Unique identifier for this persistence operation.</param>
/// <param name="Error">The error that occurred during persistence.</param>
/// <param name="Duration">The duration before the persistence failed.</param>
public sealed record PersistenceFailedAction(
    string PersistenceId,
    string Error,
    TimeSpan Duration);
