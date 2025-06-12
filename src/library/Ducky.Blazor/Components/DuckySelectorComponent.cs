// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

/// <summary>
/// A base component class for Ducky components that manages multiple state slices through a selector pattern.
/// </summary>
/// <typeparam name="TViewModel">The type of the view model derived from state slices.</typeparam>
public abstract class DuckySelectorComponent<TViewModel> : ComponentBase, IDisposable
    where TViewModel : notnull
{
    private TViewModel? _currentViewModel;
    private bool _disposed;

    /// <summary>
    /// Gets or sets the store that manages the application state.
    /// </summary>
    [Inject]
    public required IStore Store { get; set; }

    /// <summary>
    /// Gets or sets the dispatcher used to dispatch actions to the store.
    /// </summary>
    [Inject]
    public required IDispatcher Dispatcher { get; set; }

    /// <summary>
    /// Gets or sets the logger used for logging information.
    /// </summary>
    [Inject]
    public required ILogger<DuckySelectorComponent<TViewModel>> Logger { get; set; }

    /// <summary>
    /// Gets the name of the component.
    /// </summary>
    protected string ComponentName
        => GetType().Name;

    /// <summary>
    /// Gets the current view model of the component.
    /// </summary>
    protected TViewModel ViewModel
    {
        get
        {
            if (_currentViewModel is null)
            {
                UpdateViewModel();
            }

            return _currentViewModel!;
        }
    }

    /// <summary>
    /// Selects and constructs a view model from the current root state.
    /// This method should be implemented to specify which state slices to use.
    /// </summary>
    /// <param name="rootState">The current root state containing all slices.</param>
    /// <returns>The view model constructed from the selected state slices.</returns>
    protected abstract TViewModel Select(IRootState rootState);

    /// <summary>
    /// Determines whether the component should re-render based on view model changes.
    /// Override this method to implement custom equality logic.
    /// </summary>
    /// <param name="previous">The previous view model.</param>
    /// <param name="current">The current view model.</param>
    /// <returns>True if the component should re-render; otherwise, false.</returns>
    protected virtual bool ShouldRender(TViewModel? previous, TViewModel current)
    {
        // Default implementation uses standard equality
        return !EqualityComparer<TViewModel>.Default.Equals(previous, current);
    }

    /// <summary>
    /// Updates the current view model from the store.
    /// </summary>
    private void UpdateViewModel()
    {
        _currentViewModel = Select(Store.CurrentState);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the component and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">If true, the method has been called directly or indirectly by a user's code.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Store.StateChanged -= OnStateChanged;
        }

        _disposed = true;
    }

    /// <summary>
    /// Invoked after the state subscription has been established.
    /// This method is intended to be overridden by derived classes.
    /// </summary>
    protected virtual void OnAfterSubscribed()
    {
    }

    /// <inheritdoc />
    protected sealed override void OnInitialized()
    {
        Logger.ComponentInitializing(ComponentName);

        base.OnInitialized();

        // Subscribe to state changes
        Store.StateChanged += OnStateChanged;
        OnAfterSubscribed();

        // Initialize the current view model
        UpdateViewModel();

        Logger.ComponentInitialized(ComponentName);
    }

    /// <summary>
    /// Dispatches an action to the store.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    protected void Dispatch(object action)
    {
        Dispatcher.Dispatch(action);
    }

    private void OnStateChanged(object? sender, StateChangedEventArgs e)
    {
        TViewModel? previousViewModel = _currentViewModel;
        UpdateViewModel();

        // Only re-render if the view model actually changed
        if (!ShouldRender(previousViewModel, _currentViewModel!))
        {
            return;
        }

        InvokeAsync(StateHasChanged);
        Logger.ComponentRefreshed(ComponentName);
    }
}
