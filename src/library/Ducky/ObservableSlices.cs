// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using ObservableCollections;
using R3;

namespace Ducky;

/// <summary>
/// Manages a collection of observable slices and provides an observable root state.
/// </summary>
public sealed class ObservableSlices : IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ObservableDictionary<string, ISlice> _slices = [];
    private readonly ReactiveProperty<IRootState> _rootState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableSlices"/> class.
    /// </summary>
    public ObservableSlices()
    {
        // Create the root state observable
        _rootState = new ReactiveProperty<IRootState>(CreateRootState());

        // Subscribe to slice changes
        _slices.ObserveAdd().Select(ev => ev.Value)
            .Merge(_slices.ObserveRemove().Select(ev => ev.Value))
            .Merge(_slices.ObserveReplace().Select(ev => ev.NewValue))
            .Select(IRootState (_) => CreateRootState())
            .Subscribe(_rootState.AsObserver())
            .AddTo(_subscriptions);

        // Create the RootStateObservable
        RootStateObservable = _rootState.ToReadOnlyReactiveProperty();
    }

    /// <summary>
    /// Gets an enumerable collection of all registered slices.
    /// </summary>
    public IEnumerable<ISlice> AllSlices
        => _slices.Select(pair => pair.Value);

    /// <summary>
    /// Gets an observable that emits the root state whenever a slice is added, removed, or replaced.
    /// </summary>
    public ReadOnlyReactiveProperty<IRootState> RootStateObservable { get; }

    /// <summary>
    /// Adds a new slice with the specified key and data.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        _slices[slice.GetKey()] = slice;

        slice.StateUpdated
            .Subscribe(_ => UpdateRootState())
            .AddTo(_subscriptions);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _rootState.Dispose();
        _subscriptions.Dispose();
        _slices.Clear();
    }

    /// <summary>
    /// Creates a new root state based on the current slices.
    /// </summary>
    /// <returns>A new <see cref="RootState"/> object.</returns>
    private RootState CreateRootState()
    {
        ImmutableSortedDictionary<string, object> state = _slices.ToImmutableSortedDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetState());

        return new RootState(state);
    }

    /// <summary>
    /// Updates the root state.
    /// </summary>
    private void UpdateRootState()
    {
        _rootState.OnNext(CreateRootState());
    }
}
