﻿// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using R3;

namespace R3dux;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public sealed class R3duxStore
    : Observable<IRootState>, IStore, IDisposable
{
    private static readonly ILogger<R3duxStore>? _logger
        = LoggerProvider.CreateLogger<R3duxStore>();

    private readonly CompositeDisposable _stateUpdateSubscriptions = [];
    private readonly CompositeDisposable _sliceSubscriptions = [];
    private readonly CompositeDisposable _effectSubscriptions = [];
    private readonly ObservableSlices _slices = new();
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="R3duxStore"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to dispatch actions to the store.</param>
    public R3duxStore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        Dispatcher = dispatcher;
        Dispatcher.Dispatch(new StoreInitialized());

        _logger?.StoreInitialized();
    }

    /// <inheritdoc/>
    public IDispatcher Dispatcher { get; }

    /// <inheritdoc/>
    public Observable<IRootState> RootStateObservable
        => _slices.RootStateObservable.DistinctUntilChanged();

    /// <inheritdoc/>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        // Add the slice to the ObservableSlices collection
        _slices.AddSlice(slice);

        // Subscribe the slice to the dispatcher's action stream
        Dispatcher.ActionStream
            .Subscribe(slice.OnDispatch)
            .AddTo(_sliceSubscriptions);

        // Update the root state when a slice state is updated
        slice.StateUpdated
            .Subscribe(_ => _slices.ReplaceSlice(slice.GetKey(), slice))
            .AddTo(_stateUpdateSubscriptions);

        _logger?.SliceAdded(slice.GetKey());
    }

    /// <inheritdoc/>
    public void AddSlices(params ISlice[] slices)
    {
        ArgumentNullException.ThrowIfNull(slices);

        foreach (var slice in slices)
        {
            AddSlice(slice);
        }
    }

    /// <inheritdoc/>
    public void AddEffect(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        effect
            .Handle(Dispatcher.ActionStream, RootStateObservable)
            .Subscribe(Dispatcher.Dispatch)
            .AddTo(_effectSubscriptions);

        _logger?.EffectAdded(effect.GetKey(), effect.GetAssemblyName());
    }

    /// <inheritdoc/>
    public void AddEffects(params IEffect[] effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        foreach (var effect in effects)
        {
            AddEffect(effect);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_isDisposed)
        {
            _logger?.DisposingStore();

            _stateUpdateSubscriptions.Dispose();
            _sliceSubscriptions.Dispose();
            _effectSubscriptions.Dispose();
            _slices.Dispose();

            _isDisposed = true;
        }
    }

    /// <inheritdoc/>
    protected override IDisposable SubscribeCore(Observer<IRootState> observer)
    {
        return _slices.RootStateObservable.Subscribe(observer);
    }
}
