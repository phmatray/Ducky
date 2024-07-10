using System.Collections.Immutable;
using ObservableCollections;
using R3;

namespace R3dux;

/// <summary>
/// Manages a collection of observable slices and provides an observable root state.
/// </summary>
public sealed class ObservableSlices
{
    private readonly ObservableDictionary<string, ISlice> _slices = [];
    private readonly ReactiveProperty<RootState> _rootState;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableSlices"/> class.
    /// </summary>
    public ObservableSlices()
    {
        // Create the root state observable
        _rootState = new ReactiveProperty<RootState>(CreateRootState());

        // Create the slice observables
        var sliceAdded = _slices
            .ObserveAdd()
            .Select(ev => ev.Value.Value);
        
        var sliceRemoved = _slices
            .ObserveRemove()
            .Select(ev => ev.Value.Value);
        
        var sliceReplaced = _slices
            .ObserveReplace()
            .Select(ev => ev.NewValue.Value);

        // Create the RootStateObservable
        sliceAdded
            .Merge(sliceRemoved)
            .Merge(sliceReplaced)
            .Select(_ => CreateRootState())
            .Subscribe(_rootState.AsObserver());
    }

    /// <summary>
    /// Gets an observable that emits the root state whenever a slice is added, removed, or replaced.
    /// </summary>
    public Observable<RootState> RootStateObservable
        => _rootState.AsObservable();

    /// <summary>
    /// Gets the current root state.
    /// </summary>
    public RootState RootState
        => _rootState.CurrentValue;
    
    /// <summary>
    /// Creates a new root state based on the current slices.
    /// </summary>
    /// <returns>A new <see cref="RootState"/> object.</returns>
    private RootState CreateRootState()
    {
        lock (_lock)
        {
            var state = _slices.ToImmutableSortedDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetState());

            return new RootState(state);
        }
    }

    /// <summary>
    /// Adds a new slice with the specified key and data.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    public void AddSlice(ISlice slice)
    {
        lock (_lock)
        {
            _slices[slice.GetKey()] = slice;
        }
    }

    /// <summary>
    /// Removes the slice with the specified key.
    /// </summary>
    /// <param name="key">The key of the slice to remove.</param>
    public void RemoveSlice(string key)
    {
        lock (_lock)
        {
            if (_slices.ContainsKey(key))
            {
                _slices.Remove(key);
            }
        }
    }
    
    /// <summary>
    /// Replaces the slice with the specified key.
    /// </summary>
    /// <param name="key">The key of the slice to replace.</param>
    /// <param name="slice">The new slice to add.</param>
    public void ReplaceSlice(string key, ISlice slice)
    {
        lock (_lock)
        {
            _slices[key] = slice;
        }
    }
}