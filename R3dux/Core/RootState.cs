using R3dux.Exceptions;

namespace R3dux;

public record RootState
{
    private readonly Dictionary<string, object> _state = [];

    public RootState()
    {
    }

    public RootState(List<ISlice> slices)
    {
        ArgumentNullException.ThrowIfNull(slices);
        slices.ForEach(slice => _state.Add(slice.Key, slice.InitialState));
    }

    public object this[string key]
    {
        get => Select<object>(key);
        set => _state[key] = value;
    }

    public TState Select<TState>(string key)
        where TState : notnull
        => _state[key] is not TState state
            ? throw new R3duxException($"State with key '{key}' is not of type '{typeof(TState).Name}'.")
            : state;

    public bool ContainsKey(string key)
        => _state.ContainsKey(key);
}