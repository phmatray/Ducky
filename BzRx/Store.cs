using Microsoft.Extensions.DependencyInjection;
using R3;

namespace BzRx;

public class Store<T> : Observer<IAction>, Observable<T>
{
    private readonly Observable<T> _stateObservable;
    private readonly ActionsSubject _actionsObserver;
    private readonly ReducerManager _reducerManager;

    public Store(
        StateObservable stateObservable,
        ActionsSubject actionsObserver,
        ReducerManager reducerManager)
    {
        _stateObservable = stateObservable;
        _actionsObserver = actionsObserver;
        _reducerManager = reducerManager;
        State = stateObservable.State;
    }

    public Observable<T> State { get; }

    public IDisposable Subscribe(Observer<T> observer)
    {
        return _stateObservable.Subscribe(observer);
    }

    public void OnCompleted()
    {
        _actionsObserver.OnCompleted();
    }

    public void OnError(Exception error)
    {
        _actionsObserver.OnErrorResume(error);
    }

    public void OnNext(IAction value)
    {
        _actionsObserver.OnNext(value);
    }

    public Observable<K> Select<K>(Func<T, K> selector)
    {
        return _stateObservable
            .Select(selector)
            .DistinctUntilChanged();
    }

    public Observable<K> Select<K, Props>(Func<T, Props, K> selector, Props props)
    {
        return _stateObservable
            .Select(state => selector(state, props))
            .DistinctUntilChanged();
    }

    public Observable<object> Select(params string[] path)
    {
        return _stateObservable
            .Select(state => GetValueByPath(state, path))
            .DistinctUntilChanged();
    }

    public void Dispatch<V>(V action) where V : IAction
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action), "Actions must be objects");
        }

        _actionsObserver.OnNext(action);
    }

    public void AddReducer<S, A>(string key, ActionReducer<S, A> reducer) where A : IAction
    {
        _reducerManager.AddReducer(key, reducer);
    }

    public void RemoveReducer(string key)
    {
        _reducerManager.RemoveReducer(key);
    }

    private static object GetValueByPath(object obj, string[] path)
    {
        foreach (var part in path)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var property = type.GetProperty(part);
            if (property == null) return null;
            obj = property.GetValue(obj, null);
        }

        return obj;
    }
}

public static class StoreProvider
{
    public static void AddStore<T>(this IServiceCollection services)
    {
        services.AddSingleton<Store<T>>();
        services.AddSingleton<Observable<T>>(provider => provider.GetRequiredService<Store<T>>());
        services.AddSingleton<Observer<IAction>>(provider => provider.GetRequiredService<Store<T>>());
    }
}

public static class SelectExtensions
{
    public static Observable<K> Select<T, K>(this Observable<T> source, Func<T, K> selector)
    {
        return source
            .Select(selector)
            .DistinctUntilChanged();
    }

    public static Observable<K> Select<T, Props, K>(
        this Observable<T> source,
        Func<T, Props, K> selector,
        Props props)
    {
        return source
            .Select(state => selector(state, props))
            .DistinctUntilChanged();
    }

    public static Observable<object> Select<T>(this Observable<T> source, params string[] path)
    {
        return source
            .Select(state => GetValueByPath(state, path))
            .DistinctUntilChanged();
    }

    private static object? GetValueByPath(object obj, string[] path)
    {
        foreach (var part in path)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var property = type.GetProperty(part);
            if (property == null) return null;
            obj = property.GetValue(obj, null);
        }

        return obj;
    }
}