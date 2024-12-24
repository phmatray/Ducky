// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// Provides extension methods for working with observables.
/// </summary>
public static class ReactiveSelectorExtensions
{
    /// <summary>
    /// Projects each element of an observable sequence into a new observable sequence and then switches to the latest observable sequence.
    /// </summary>
    /// <typeparam name="TInput">The type of elements in the source observable sequence.</typeparam>
    /// <typeparam name="TOutput">The type of elements in the projected observable sequences.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each element in the input sequence.</param>
    /// <returns>An observable sequence whose elements are the result of invoking the transform function on each element of the source.</returns>
    public static Observable<TOutput> SwitchSelect<TInput, TOutput>(
        this Observable<TInput> source,
        Func<TInput, Observable<TOutput>> selector)
    {
        return source.Select(selector).Switch();
    }

    /// <summary>
    /// Projects each element of an observable sequence into a new observable sequence and concatenates the resulting observable sequences.
    /// </summary>
    /// <typeparam name="TInput">The type of elements in the source observable sequence.</typeparam>
    /// <typeparam name="TOutput">The type of elements in the projected observable sequences.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each element in the input sequence.</param>
    /// <returns>An observable sequence whose elements are the result of invoking the transform function on each element of the source and concatenating the resulting sequences.</returns>
    public static Observable<TOutput> ConcatSelect<TInput, TOutput>(
        this Observable<TInput> source,
        Func<TInput, Observable<TOutput>> selector)
    {
        return source.Select(selector).Concat();
    }

    /// <summary>
    /// Projects each element of an observable sequence into a new observable sequence and merges the resulting observable sequences.
    /// </summary>
    /// <typeparam name="TInput">The type of elements in the source observable sequence.</typeparam>
    /// <typeparam name="TOutput">The type of elements in the projected observable sequences.</typeparam>
    /// <param name="source">The source observable sequence.</param>
    /// <param name="selector">A transform function to apply to each element in the input sequence.</param>
    /// <returns>An observable sequence whose elements are the result of invoking the transform function on each element of the source and merging the resulting sequences.</returns>
    public static Observable<TOutput> MergeSelect<TInput, TOutput>(
        this Observable<TInput> source,
        Func<TInput, Observable<TOutput>> selector)
    {
        return source.Select(selector).Merge();
    }
}
