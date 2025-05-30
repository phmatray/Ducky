namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Defines methods for persisting and loading application state.
/// </summary>
/// <typeparam name="TState">The type of the state to persist.</typeparam>
public interface IPersistenceProvider<TState>
    where TState : class
{
    /// <summary>
    /// Asynchronously loads the persisted state.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous load operation. The task result contains the loaded state,
    /// or <c>null</c> if no state is persisted.
    /// </returns>
    Task<TState?> LoadAsync();

    /// <summary>
    /// Asynchronously saves the specified state.
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task SaveAsync(TState state);
}

/// <summary>
/// Enhanced persistence provider interface with metadata and versioning support.
/// </summary>
/// <typeparam name="TState">The type of the state to persist.</typeparam>
public interface IEnhancedPersistenceProvider<TState> where TState : class
{
    /// <summary>
    /// Asynchronously loads the persisted state with metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous load operation. The task result contains the loaded state container,
    /// or <c>null</c> if no state is persisted.
    /// </returns>
    Task<PersistedStateContainer<TState>?> LoadWithMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously saves the specified state with metadata.
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <param name="metadata">The metadata to associate with the state.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous save operation result.</returns>
    Task<PersistenceResult> SaveWithMetadataAsync(
        TState state,
        PersistenceMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes the persisted state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous clear operation result.</returns>
    Task<PersistenceResult> ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if persisted state exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous check operation. True if state exists, false otherwise.</returns>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the size of the persisted state in bytes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous size calculation. Returns -1 if size cannot be determined.</returns>
    Task<long> GetSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously migrates persisted state from an older version.
    /// </summary>
    /// <param name="fromVersion">The version to migrate from.</param>
    /// <param name="toVersion">The version to migrate to.</param>
    /// <param name="migrationFunc">The migration function to apply.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task that represents the asynchronous migration operation result.</returns>
    Task<PersistenceResult> MigrateAsync(
        int fromVersion,
        int toVersion,
        Func<TState, TState> migrationFunc,
        CancellationToken cancellationToken = default);
}
