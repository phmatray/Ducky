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
