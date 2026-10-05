namespace Ducky.Blazor;

/// <summary>
/// The persistence status of the store, held by the library slice <c>@ducky/persistence</c> and changed only by the
/// drainer, so a loading UI selects it like any state.
/// </summary>
/// <param name="Status">Where hydration stands.</param>
public sealed record PersistenceState(PersistenceStatus Status);

/// <summary>Where the hydration of persisted slices stands.</summary>
public enum PersistenceStatus
{
    /// <summary>Not attempted: a non-interactive store with no readable slice.</summary>
    NotStarted,

    /// <summary>Reading persisted slices.</summary>
    Hydrating,

    /// <summary>Done, or nothing to read.</summary>
    Hydrated,

    /// <summary>The read failed or timed out; persistence resumes.</summary>
    Failed,
}
