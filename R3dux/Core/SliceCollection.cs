using System.Collections;

namespace R3dux;

public class SliceCollection : IEnumerable<ISlice>
{
    private readonly Dictionary<string, ISlice> _slices = new();

    public void Add<TState>(Slice<TState> slice)
        where TState : notnull, new()
    {
        ArgumentNullException.ThrowIfNull(slice);
        _slices.Add(slice.Key, slice);
    }

    public IEnumerable<IReducer<object>> GetReducers()
        => _slices.Values.Select(slice => slice.Reducers as IReducer<object>)!;

    public IEnumerable<IEffect> GetEffects()
        => _slices.Values.SelectMany(slice => slice.Effects);

    public RootState GetInitialState()
    {
        var rootState = new RootState();
        
        foreach (var slice in _slices.Values)
        {
            rootState[slice.Key] = slice.InitialState;
        }
        
        return rootState;
    }

    public IEnumerator<ISlice> GetEnumerator()
        => _slices.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}