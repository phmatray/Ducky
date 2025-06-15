namespace Ducky.Blazor.Middlewares.Persistence;

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
