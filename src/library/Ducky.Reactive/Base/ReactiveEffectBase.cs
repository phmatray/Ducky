// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive;

/// <summary>
/// Enhanced base class for reactive effects with lifecycle hooks and error handling.
/// </summary>
public abstract class ReactiveEffectBase : ReactiveEffect, IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private readonly Subject<Exception> _errors = new();
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>
    /// Gets the observable stream of errors from this effect.
    /// </summary>
    public IObservable<Exception> Errors => _errors.AsObservable();

    /// <summary>
    /// Gets a value indicating whether this effect has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets a value indicating whether this effect has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Called when the effect is first initialized.
    /// Override to perform one-time setup.
    /// </summary>
    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when the effect is being disposed.
    /// Override to perform cleanup.
    /// </summary>
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Called when an error occurs in the effect.
    /// Override to provide custom error handling.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    /// <returns>True to continue processing, false to stop.</returns>
    protected virtual bool OnError(Exception error)
    {
        _errors.OnNext(error);
        return true; // Continue by default
    }

    /// <summary>
    /// Adds a disposable to be cleaned up when the effect is disposed.
    /// </summary>
    /// <param name="disposable">The disposable to add.</param>
    protected void AddDisposable(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }

    /// <summary>
    /// Handles the effect with automatic error handling and lifecycle management.
    /// </summary>
    public sealed override IObservable<object> Handle(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        return Observable.Create<object>(async observer =>
        {
            if (!_isInitialized)
            {
                try
                {
                    await OnInitializeAsync().ConfigureAwait(false);
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    return Disposable.Empty;
                }
            }

            IDisposable subscription = HandleCore(actions, stateProvider)
                .Catch<object, Exception>(error =>
                {
                    if (OnError(error))
                    {
                        return Observable.Empty<object>();
                    }

                    return Observable.Throw<object>(error);
                })
                .Subscribe(observer);

            return new CompositeDisposable(subscription, _disposables);
        });
    }

    /// <summary>
    /// Core effect implementation. Override this method to implement your effect logic.
    /// </summary>
    /// <param name="actions">The stream of actions.</param>
    /// <param name="stateProvider">The stream of state provider.</param>
    /// <returns>An observable of new actions to dispatch.</returns>
    protected abstract IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider);

    /// <summary>
    /// Disposes the effect and performs cleanup.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the effect.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        if (disposing)
        {
            OnDisposeAsync().GetAwaiter().GetResult();
            _disposables.Dispose();
            _errors.OnCompleted();
            _errors.Dispose();
        }

        _isDisposed = true;
    }
}
