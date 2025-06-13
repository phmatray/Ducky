using System.Collections.Immutable;
using System.Text.Json;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides initial state from persisted storage.
/// </summary>
public interface IInitialStateProvider
{
    /// <summary>
    /// Gets the initial state for a slice, or null if not available.
    /// </summary>
    TState? GetInitialState<TState>(string sliceKey) where TState : class;
}

/// <summary>
/// Implementation that provides initial state from a loaded state dictionary.
/// </summary>
public class InitialStateProvider : IInitialStateProvider
{
    private readonly ImmutableSortedDictionary<string, object> _loadedState;

    public InitialStateProvider(ImmutableSortedDictionary<string, object>? loadedState = null)
    {
        _loadedState = loadedState ?? ImmutableSortedDictionary<string, object>.Empty;
    }

    public TState? GetInitialState<TState>(string sliceKey) where TState : class
    {
        if (_loadedState.TryGetValue(sliceKey, out var state))
        {
            // Handle JsonElement deserialization
            if (state is JsonElement jsonElement)
            {
                try
                {
                    return jsonElement.Deserialize<TState>();
                }
                catch
                {
                    return null;
                }
            }

            // Direct cast if already the right type
            if (state is TState typedState)
            {
                return typedState;
            }
        }

        return null;
    }
}

/// <summary>
/// Extension methods for SliceReducers to use persisted initial state.
/// </summary>
public static class SliceReducersPersistenceExtensions
{
    /// <summary>
    /// Gets initial state from persistence or falls back to default.
    /// </summary>
    public static TState GetInitialStateWithPersistence<TState>(
        this SliceReducers<TState> reducers,
        IServiceProvider serviceProvider)
        where TState : class, IState, new()
    {
        var stateProvider = serviceProvider.GetService(typeof(IInitialStateProvider)) as IInitialStateProvider;
        if (stateProvider != null)
        {
            var persistedState = stateProvider.GetInitialState<TState>(reducers.GetKey());
            if (persistedState != null)
            {
                return persistedState;
            }
        }

        // Fall back to default initial state
        return reducers.GetInitialState();
    }
}