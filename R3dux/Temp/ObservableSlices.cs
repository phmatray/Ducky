using ObservableCollections;
using R3;

namespace R3dux.Temp;

/// <summary>
/// Manages a collection of observable slices and provides an observable root state.
/// </summary>
public sealed class ObservableSlices
{
    private readonly ObservableDictionary<string, ISlice> _slices = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets an observable that emits the root state whenever a slice is added, removed, or replaced.
    /// </summary>
    public Observable<RootState> RootStateObservable { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableSlices"/> class.
    /// </summary>
    public ObservableSlices()
    {
        // Create the slice observables
        var sliceAdded = _slices
            .ObserveAdd()
            .Select(ev => ev.Value.Value);
        
        var sliceRemoved = _slices
            .ObserveRemove()
            .Select(ev => ev.Value.Value);

        // Create the RootStateObservable
        RootStateObservable = sliceAdded
            .Merge(sliceRemoved)
            .Select(kvp => CreateRootState());
    }

    /// <summary>
    /// Creates a new root state based on the current slices.
    /// </summary>
    /// <returns>A new <see cref="RootState"/> object.</returns>
    private RootState CreateRootState()
    {
        lock (_lock)
        {
            var state = _slices
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetState())
                .AsReadOnly();

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
}
