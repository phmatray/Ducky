namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Enhanced persistence provider interface with metadata support.
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
}
