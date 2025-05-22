using Blazored.LocalStorage;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides persistence for application state using browser localStorage.
/// </summary>
/// <typeparam name="TState">The type of the state to persist.</typeparam>
public class LocalStoragePersistenceProvider<TState> : IPersistenceProvider<TState>
    where TState : class
{
    private readonly ILocalStorageService _localStorage;
    private readonly string _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStoragePersistenceProvider{TState}"/> class.
    /// </summary>
    /// <param name="localStorage">The local storage service.</param>
    /// <param name="key">The key used to store the state in localStorage. If null, a default key is used.</param>
    public LocalStoragePersistenceProvider(
        ILocalStorageService localStorage, string? key = null)
    {
        _localStorage = localStorage;
        _key = key ?? $"redux:{typeof(TState).FullName}";
    }

    /// <summary>
    /// Asynchronously loads the persisted state from localStorage.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous load operation. The task result contains the loaded state,
    /// or <c>null</c> if no state is persisted.
    /// </returns>
    public async Task<TState?> LoadAsync()
    {
        return await _localStorage
            .GetItemAsync<TState>(_key)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously saves the specified state to localStorage.
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveAsync(TState state)
    {
        await _localStorage
            .SetItemAsync(_key, state)
            .ConfigureAwait(false);
    }
}
