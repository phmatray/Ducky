namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Metadata associated with persisted state.
/// </summary>
public class PersistenceMetadata
{
    /// <summary>
    /// Gets or sets the version of the persisted state schema.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the timestamp when the state was persisted.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the application version that persisted the state.
    /// </summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>
    /// Gets or sets the user agent or platform information.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the persisted state for integrity verification.
    /// </summary>
    public string? Checksum { get; set; }
}

/// <summary>
/// Container for persisted state with associated metadata.
/// </summary>
/// <typeparam name="TState">The type of the persisted state.</typeparam>
public class PersistedStateContainer<TState> where TState : class
{
    /// <summary>
    /// Gets or sets the persisted state data.
    /// </summary>
    public TState? State { get; set; }

    /// <summary>
    /// Gets or sets the metadata associated with the persisted state.
    /// </summary>
    public PersistenceMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Result of a persistence operation.
/// </summary>
public class PersistenceResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if the operation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PersistenceResult Successful() => new() { Success = true };

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    public static PersistenceResult Failed(string error) => new() { Success = false, Error = error };
}
