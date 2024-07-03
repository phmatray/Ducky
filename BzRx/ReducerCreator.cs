namespace BzRx;

public static class ReducerCreator
{
    public class ReducerTypes<TState, TAction>
    {
        public OnReducer<TState, TAction> Reducer { get; }
        public IEnumerable<string> Types { get; }

        public ReducerTypes(OnReducer<TState, TAction> reducer, IEnumerable<string> types)
        {
            Reducer = reducer;
            Types = types;
        }
    }

    public delegate TResult OnReducer<in TState, in TAction, out TResult>(
        TState state, TAction action)
        where TAction : IAction;

    public static ReducerTypes<TState, IAction> On<TState>(
        OnReducer<TState, IAction, TState> reducer,
        params IActionCreator[] actionCreators)
    {
        var types = actionCreators.Select(ac => ac.Type);
        return new ReducerTypes<TState, IAction>(reducer, types);
    }

    public static ActionReducer<TState, TAction> CreateReducer<TState, TAction>(
        TState initialState,
        params ReducerTypes<TState, IAction>[] ons)
        where TAction : IAction
    {
        var map = new Dictionary<string, OnReducer<TState, IAction, TState>>();

        foreach (var on in ons)
        {
            foreach (var type in on.Types)
            {
                if (map.ContainsKey(type))
                {
                    var existingReducer = map[type];
                    map[type] = (state, action) => on.Reducer(existingReducer(state, action), action);
                }
                else
                {
                    map[type] = on.Reducer;
                }
            }
        }

        return (state, action) =>
        {
            if (state == null) state = initialState;
            if (map.TryGetValue(action.Type, out var reducer))
            {
                return reducer(state, action);
            }

            return state;
        };
    }
}