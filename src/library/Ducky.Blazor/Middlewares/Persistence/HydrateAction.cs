namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Action used to hydrate the store with persisted state.
/// </summary>
/// <typeparam name="TState">The type of the state.</typeparam>
public sealed record HydrateAction<TState>(TState State)
    where TState : class;
