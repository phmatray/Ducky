// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;
using R3dux.Abstractions;

namespace R3dux.Extensions.Operators;

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
    public static Observable<TAction> OfType<TAction>(
        this Observable<IAction> source)
        where TAction : IAction
    {
        return source.OfType<IAction, TAction>();
    }

    /// <summary>
    /// Combines the observable sequence with the state of a slice and projects the result into a new form.
    /// </summary>
    /// <typeparam name="TState">The type of the state.</typeparam>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="rootStateObs">The observable sequence of the root state.</param>
    /// <param name="sliceKey">The key of the slice to select.</param>
    /// <returns>An observable sequence of StateActionPair.</returns>
    public static Observable<StateActionPair<TState, TAction>> WithSliceState<TState, TAction>(
        this Observable<TAction> source,
        Observable<IRootState> rootStateObs,
        string? sliceKey = null)
        where TAction : IAction
        where TState : notnull
    {
        return source.WithLatestFrom(
            rootStateObs,
            (action, rootState) =>
            {
                var sliceState = sliceKey is null
                    ? rootState.GetSliceState<TState>()
                    : rootState.GetSliceState<TState>(sliceKey);

                return new StateActionPair<TState, TAction>(sliceState, action);
            });
    }

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
    {
        return source
            .Select(selector)
            .Cast<TResult, IAction>();
    }

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
    {
        return source.Catch<TSource, Exception>(ex => source.Select(_ => selector(ex)));
    }

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
    public static Observable<IAction> InvokeService<TAction, TResult>(
        this Observable<TAction> source,
        Func<TAction, ValueTask<TResult>> serviceCall,
        Func<TResult, IAction> successSelector,
        Func<Exception, IAction> errorSelector)
    {
        return source.SelectAwait(async (action, ct) =>
        {
            try
            {
                var result = await serviceCall(action);
                return successSelector(result);
            }
            catch (Exception ex)
            {
                return errorSelector(ex);
            }
        });
    }

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
    {
        return source.Do(_ => Console.WriteLine(message));
    }
}
