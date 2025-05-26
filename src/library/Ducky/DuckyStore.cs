// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;
using R3;

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public sealed class DuckyStore : IStore, IDisposable
{
    private readonly IDispatcher _dispatcher;
    private readonly ActionPipeline _pipeline;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ObservableSlices _slices = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuckyStore"/> class.
    /// </summary>
    /// <param name="dispatcher">The dispatcher used to enqueue actions.</param>
    /// <param name="pipeline">The reactive action pipeline that processes actions.</param>
    /// <param name="eventPublisher">The event publisher for store events.</param>
    /// <param name="slices">The initial collection of slices to register.</param>
    public DuckyStore(
        IDispatcher dispatcher,
        ActionPipeline pipeline,
        IStoreEventPublisher eventPublisher,
        IEnumerable<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(eventPublisher);
        ArgumentNullException.ThrowIfNull(slices);

        _dispatcher = dispatcher;
        _pipeline = pipeline;
        _eventPublisher = eventPublisher;

        List<string> sliceKeys = [];
        
        // Register slices
        foreach (ISlice slice in slices)
        {
            _slices.AddSlice(slice);
            string sliceKey = slice.GetKey();
            sliceKeys.Add(sliceKey);
            _eventPublisher.Publish(new SliceAddedEventArgs(sliceKey, slice.GetType()));
        }

        // Subscribe all slices to the BEFORE pipeline (standard: state update/reducer logic)
        _pipeline
            .SubscribeBefore(new SliceObserver(ctx =>
            {
                if (ctx.IsAborted)
                {
                    return;
                }

                foreach (ISlice slice in _slices.AllSlices)
                {
                    slice.OnDispatch(ctx.Action);
                }
            }))
            .AddTo(_subscriptions);

        // Subscribe all slices to the AFTER pipeline (for post-processing/effects)
        _pipeline
            .SubscribeAfter(new SliceObserver(_ =>
            {
                // Most often used for effects, not for mutation.
                // Example: logging, triggers, analytics, etc.
                // _slices.Dispatch(ctx.Action);
            }))
            .AddTo(_subscriptions);

        // Dispatch initial action
        _dispatcher.Dispatch(new StoreInitialized());
        
        // Publish store initialized event
        _eventPublisher.Publish(new StoreInitializedEventArgs(sliceKeys.Count, sliceKeys));
    }

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<IRootState> RootStateObservable
        => _slices.RootStateObservable;

    /// <inheritdoc/>
    public IRootState CurrentState
        => RootStateObservable.CurrentValue;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        TimeSpan uptime = DateTime.UtcNow - _startTime;
        _eventPublisher.Publish(new StoreDisposingEventArgs(uptime));

        _subscriptions.Dispose();
        _pipeline.Dispose();
        _slices.Dispose();

        _isDisposed = true;
    }
}
