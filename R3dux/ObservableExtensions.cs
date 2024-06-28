using R3;

namespace R3dux;

public static class ObservableExtensions
{
    public static Observable<TAction> FilterActions<TAction>(
        this Observable<IAction> source)
        where TAction : IAction
        => source.OfType<IAction, TAction>();
    
    /// <summary>
    /// Projects each element of an observable sequence into a new form and casts it to IAction.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements, which must implement IAction.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>An observable sequence of IAction.</returns>
    public static Observable<IAction> SelectAction<TSource, TResult>(
        this Observable<TSource> source,
        Func<TSource, TResult> selector)
        where TResult : IAction
        => source.Select(selector)
            .Cast<TResult, IAction>();
    
    public static Observable<TSource> CatchAction<TSource>(
        this Observable<TSource> source,
        Func<Exception, TSource> selector)
        where TSource : IAction
        => source.Catch<TSource, Exception>(ex 
            => Observable.Return(selector(ex)));
    
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
    
    public static Observable<TSource> LogMessage<TSource>(
        this Observable<TSource> source,
        string message)
        => source.Do(_ => Console.WriteLine(message));
}