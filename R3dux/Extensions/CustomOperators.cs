using R3;

namespace R3dux;

/// <summary>
/// Provides custom operators for working with observable sequences in the R3dux application.
/// </summary>
public static class CustomOperators
{
    /// <summary>
    /// Filters the observable sequence to include only elements of a specified type.
    /// </summary>
    /// <typeparam name="TAction">The type of elements to filter.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <returns>An observable sequence that contains elements from the input sequence of type TAction.</returns>
    public static Observable<TAction> FilterActions<TAction>(
        this Observable<object> source)
        => source.OfType<object, TAction>();
    
    /// <summary>
    /// Projects each element of an observable sequence into a new form and casts it to IAction.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <typeparam name="TResult">The type of the result elements, which must implement IAction.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>An observable sequence of IAction.</returns>
    public static Observable<object> SelectAction<TSource, TResult>(
        this Observable<TSource> source,
        Func<TSource, TResult> selector)
        => source.Select(selector)
            .Cast<TResult, object>();
    
    /// <summary>
    /// Catches exceptions in the observable sequence and replaces the exception with a specified value.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each exception.</param>
    /// <returns>An observable sequence containing the source elements and replacing exceptions with the result of the selector function.</returns>
    public static Observable<TSource> CatchAction<TSource>(
        this Observable<TSource> source,
        Func<Exception, TSource> selector)
        => source.Catch<TSource, Exception>(ex 
            => Observable.Return(selector(ex)));

    /// <summary>
    /// Invokes a service call for each element in the observable sequence, returning a sequence of actions based on the result or error.
    /// </summary>
    /// <typeparam name="TAction">The type of the source elements, which are actions to be processed.</typeparam>
    /// <typeparam name="TResult">The type of the result from the service call.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="serviceCall">A function that performs the service call.</param>
    /// <param name="successSelector">A function that selects the success action based on the result.</param>
    /// <param name="errorSelector">A function that selects the error action based on the exception.</param>
    /// <returns>An observable sequence of actions resulting from the service call.</returns>
    public static Observable<object> InvokeService<TAction, TResult>(
        this Observable<TAction> source,
        Func<TAction, ValueTask<TResult>> serviceCall,
        Func<TResult, object> successSelector,
        Func<Exception, object> errorSelector)
        => source.SelectMany(action
            => serviceCall(action)
                .ToObservable()
                .Select(successSelector)
                .CatchAction(errorSelector));
    
    /// <summary>
    /// Logs a message to the console for each element in the observable sequence.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="message">The message to log to the console.</param>
    /// <returns>The source sequence with added side-effects of logging each element.</returns>
    public static Observable<TSource> LogMessage<TSource>(
        this Observable<TSource> source,
        string message)
        => source.Do(_ => Console.WriteLine(message));
}