using R3;

namespace R3dux;

public static class ObservableExtensions
{
    public static Observable<TAction> FilterActions<TAction>(
        this Observable<IAction> source)
        where TAction : IAction
        => source.OfType<IAction, TAction>();

    public static Observable<IAction> InvokeService<TAction, TResult>(
        this Observable<TAction> source,
        Func<TAction, ValueTask<TResult>> serviceCall,
        Func<TResult, IAction> successSelector)
        where TAction : IAction
        => source.SelectMany(action 
            => serviceCall(action).ToObservable().Select(successSelector));

    public static Observable<IAction> InvokeService<TAction, TResult>(
        this Observable<TAction> source,
        Func<TAction, ValueTask<TResult>> serviceCall,
        Func<TResult, IAction> successSelector,
        Func<Exception, IAction> errorSelector)
        where TAction : IAction
        => source.InvokeService(serviceCall, successSelector)
            .CatchAction(errorSelector);
    
    public static Observable<T> CatchAction<T>(
        this Observable<T> source, Func<Exception, T> selector)
        where T : IAction
        => source.Catch<T, Exception>(ex 
            => Observable.Return(selector(ex)));
    
    public static Observable<T> LogMessage<T>(
        this Observable<T> source, string message)
        => source.Do(_ => Console.WriteLine(message));
}