// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using R3;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public sealed class DuckyStore : IStore, IDisposable
{
    private static readonly ILogger<DuckyStore>? Logger =
        LoggerProvider.CreateLogger<DuckyStore>();

    private readonly IDispatcher _dispatcher;
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ObservableSlices _slices = new();
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStore"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to dispatch actions to the store.</param>
    public DuckyStore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _dispatcher.Dispatch(new StoreInitialized());

        Logger?.StoreInitialized();
    }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<IRootState> RootStateObservable
        => _slices.RootStateObservable;

    /// <inheritdoc/>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        // Add the slice to the ObservableSlices collection
        _slices.AddSlice(slice);

        // Subscribe the slice to the dispatcher's action stream
        _dispatcher.ActionStream
            .Subscribe(slice.OnDispatch)
            .AddTo(_subscriptions);

        Logger?.SliceAdded(slice.GetKey());
    }

    /// <inheritdoc/>
    public void AddSlices(params IEnumerable<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);

        foreach (ISlice slice in slices)
        {
            AddSlice(slice);
        }
    }

    /// <inheritdoc/>
    public void AddEffect(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        effect.SetDispatcher(_dispatcher);

        _dispatcher.ActionStream
            .Where(effect.CanHandle)
            .Subscribe(action => effect.HandleAsync(action, RootStateObservable.CurrentValue))
            .AddTo(_subscriptions);

        Logger?.Log(LogLevel.Information, $"Effect added: {effect.GetType().Name}");
    }

    /// <inheritdoc/>
    public void AddEffects(params IEnumerable<IEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);

        foreach (IEffect effect in effects)
        {
            AddEffect(effect);
        }
    }

    /// <inheritdoc/>
    public void AddReactiveEffect(IReactiveEffect reactiveEffect)
    {
        ArgumentNullException.ThrowIfNull(reactiveEffect);

        reactiveEffect
            .Handle(_dispatcher.ActionStream, RootStateObservable)
            .Subscribe(_dispatcher.Dispatch)
            .AddTo(_subscriptions);

        Logger?.EffectAdded(reactiveEffect.GetKey(), reactiveEffect.GetAssemblyName());
    }

    /// <inheritdoc/>
    public void AddReactiveEffects(params IEnumerable<IReactiveEffect> reactiveEffects)
    {
        ArgumentNullException.ThrowIfNull(reactiveEffects);

        foreach (IReactiveEffect reactiveEffect in reactiveEffects)
        {
            AddReactiveEffect(reactiveEffect);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Logger?.DisposingStore();

        _subscriptions.Dispose();
        _slices.Dispose();

        _isDisposed = true;
    }
}
