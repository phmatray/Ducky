namespace BzRx;

public static class Utils
{
    public static ActionReducer<T, V> CombineReducers<T, V>(
        ActionReducerMap<T, V> reducers,
        Dictionary<string, object>? initialState = null)
        where V : IAction
    {
        initialState = initialState ?? new Dictionary<string, object>();
        var reducerKeys = reducers.Keys.ToList();
        var finalReducers = new Dictionary<string, ActionReducer<T, V>>();

        foreach (var key in reducerKeys)
        {
            if (reducers[key] != null)
            {
                finalReducers[key] = reducers[key];
            }
        }

        var finalReducerKeys = finalReducers.Keys.ToList();

        return (state, action) =>
        {
            state = state == null ? initialState.ToDictionary(k => k.Key, k => (object)k.Value) as T : state;
            var hasChanged = false;
            var nextState = new Dictionary<string, object>();

            foreach (var key in finalReducerKeys)
            {
                var reducer = finalReducers[key];
                var previousStateForKey = ((dynamic)state)[key];
                var nextStateForKey = reducer(previousStateForKey, action);

                nextState[key] = nextStateForKey;
                hasChanged = hasChanged || !Equals(nextStateForKey, previousStateForKey);
            }

            return hasChanged ? (T)(object)nextState : state;
        };
    }

    public static Dictionary<string, object> Omit<T>(Dictionary<string, object> dictionary, string keyToRemove)
    {
        return dictionary.Where(kvp => kvp.Key != keyToRemove)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static Func<A, A> Compose<A>()
    {
        return i => i;
    }

    public static Func<A, B> Compose<A, B>(Func<A, B> b)
    {
        return i => b(i);
    }

    public static Func<A, C> Compose<A, B, C>(Func<B, C> c, Func<A, B> b)
    {
        return i => c(b(i));
    }

    public static Func<A, D> Compose<A, B, C, D>(Func<C, D> d, Func<B, C> c, Func<A, B> b)
    {
        return i => d(c(b(i)));
    }

    public static Func<A, E> Compose<A, B, C, D, E>(Func<D, E> e, Func<C, D> d, Func<B, C> c, Func<A, B> b)
    {
        return i => e(d(c(b(i))));
    }

    public static Func<A, F> Compose<A, B, C, D, E, F>(Func<E, F> f, Func<D, E> e, Func<C, D> d, Func<B, C> c, Func<A, B> b)
    {
        return i => f(e(d(c(b(i)))));
    }

    public static Func<A, F> Compose<A, F>(params Delegate[] functions)
    {
        return arg =>
        {
            if (functions.Length == 0)
            {
                return (F)(object)arg;
            }

            var last = functions[functions.Length - 1];
            var rest = functions.Take(functions.Length - 1).ToArray();

            return rest
                .Reverse()
                .Aggregate(
                    last.DynamicInvoke(arg),
                    (composed, fn) => fn.DynamicInvoke(composed));
        };
    }

    public static ActionReducerFactory<T, V> CreateReducerFactory<T, V>(
        ActionReducerFactory<T, V> reducerFactory,
        MetaReducer<T, V>[]? metaReducers = null)
        where V : IAction
    {
        if (metaReducers != null && metaReducers.Length > 0)
        {
            reducerFactory = (ActionReducerFactory<T, V>)Compose(metaReducers.Cast<Delegate>().ToArray());
        }

        return (reducers, initialState) =>
        {
            var reducer = reducerFactory(reducers);
            return (state, action) =>
            {
                state = state == null ? (T)(object)initialState : state;
                return reducer(state, action);
            };
        };
    }

    public static Func<ActionReducer<T, V>, ActionReducer<T, V>> CreateFeatureReducerFactory<T, V>(
        MetaReducer<T, V>[]? metaReducers = null)
        where V : IAction
    {
        var reducerFactory = (metaReducers != null && metaReducers.Length > 0)
            ? (Func<ActionReducer<T, V>, ActionReducer<T, V>>)Compose(metaReducers.Cast<Delegate>().ToArray())
            : r => r;

        return (reducer, initialState) =>
        {
            reducer = reducerFactory(reducer);

            return (state, action) =>
            {
                state = state == null ? initialState : state;
                return reducer(state, action);
            };
        };
    }
}