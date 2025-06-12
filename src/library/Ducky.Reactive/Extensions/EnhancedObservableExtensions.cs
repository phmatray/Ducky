// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive;

/// <summary>
/// Enhanced observable extensions for common reactive patterns.
/// </summary>
public static class EnhancedObservableExtensions
{
    /// <summary>
    /// Buffers items until a condition is met, then emits all buffered items.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="condition">The condition to check for each item.</param>
    /// <returns>An observable that emits buffered items when condition is met.</returns>
    public static IObservable<IList<T>> BufferUntil<T>(
        this IObservable<T> source,
        Func<T, bool> condition)
    {
        return Observable.Create<IList<T>>(observer =>
        {
            List<T> buffer = [];

            return source.Subscribe(
                item =>
                {
                    buffer.Add(item);
                    if (!condition(item))
                    {
                        return;
                    }

                    observer.OnNext(buffer.ToList());
                    buffer.Clear();
                },
                observer.OnError,
                () =>
                {
                    if (buffer.Count > 0)
                    {
                        observer.OnNext(buffer);
                    }

                    observer.OnCompleted();
                });
        });
    }

    /// <summary>
    /// Retries an observable sequence with exponential backoff.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="maxRetries">Maximum number of retries.</param>
    /// <param name="initialDelay">Initial delay before first retry.</param>
    /// <param name="maxDelay">Maximum delay between retries.</param>
    /// <returns>An observable with retry logic.</returns>
    public static IObservable<T> RetryWithBackoff<T>(
        this IObservable<T> source,
        int maxRetries,
        TimeSpan initialDelay,
        TimeSpan? maxDelay = null)
    {
        return source.RetryWhen(errors =>
            errors.Zip(
                Observable.Range(1, maxRetries),
                (error, retryCount) => (error, retryCount))
                .SelectMany(tuple =>
                {
                    TimeSpan delay = TimeSpan.FromMilliseconds(
                        initialDelay.TotalMilliseconds * Math.Pow(2, tuple.retryCount - 1));

                    if (maxDelay.HasValue && delay > maxDelay.Value)
                    {
                        delay = maxDelay.Value;
                    }

                    return Observable.Timer(delay);
                }));
    }

    /// <summary>
    /// Ensures a minimum time between emissions.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="minInterval">Minimum interval between items.</param>
    /// <returns>An observable with rate limiting.</returns>
    public static IObservable<T> RateLimit<T>(
        this IObservable<T> source,
        TimeSpan minInterval)
    {
        return source
            .Scan(
                (lastEmit: DateTimeOffset.MinValue, value: default(T)!),
                (acc, value) => (DateTimeOffset.UtcNow, value))
            .SelectMany(item =>
            {
                TimeSpan timeSinceLastEmit = item.lastEmit == DateTimeOffset.MinValue
                    ? minInterval
                    : DateTimeOffset.UtcNow - item.lastEmit;

                TimeSpan delay = minInterval - timeSinceLastEmit;
                if (delay > TimeSpan.Zero)
                {
                    return Observable.Return(item.value).Delay(delay);
                }

                return Observable.Return(item.value);
            });
    }

    /// <summary>
    /// Switches to a fallback observable when the source fails.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="fallbackFactory">Factory for creating fallback observable.</param>
    /// <returns>An observable with fallback logic.</returns>
    public static IObservable<T> FallbackTo<T>(
        this IObservable<T> source,
        Func<Exception, IObservable<T>> fallbackFactory)
    {
        return source.Catch(fallbackFactory);
    }

    /// <summary>
    /// Collects items into batches based on count or time window.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="maxCount">Maximum items per batch.</param>
    /// <param name="maxTime">Maximum time window for a batch.</param>
    /// <returns>An observable of batches.</returns>
    public static IObservable<IList<T>> BatchWithTimeOrCount<T>(
        this IObservable<T> source,
        int maxCount,
        TimeSpan maxTime)
    {
        return source
            .Buffer(maxTime, maxCount)
            .Where(buffer => buffer.Count > 0);
    }

    /// <summary>
    /// Transforms items while preserving the original in a tuple.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="selector">The transformation function.</param>
    /// <returns>An observable of tuples containing original and transformed values.</returns>
    public static IObservable<(TSource Original, TResult Transformed)> SelectWithOriginal<TSource, TResult>(
        this IObservable<TSource> source,
        Func<TSource, TResult> selector)
    {
        return source.Select(item => (item, selector(item)));
    }

    /// <summary>
    /// Ensures that at least one item is emitted within a timeout period.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="timeout">The timeout period.</param>
    /// <param name="timeoutValue">The value to emit on timeout.</param>
    /// <returns>An observable that emits a timeout value if needed.</returns>
    public static IObservable<T> TimeoutWithDefault<T>(
        this IObservable<T> source,
        TimeSpan timeout,
        T timeoutValue)
    {
        return source.Timeout(
            timeout,
            Observable.Return(timeoutValue));
    }

    /// <summary>
    /// Shares the latest value among multiple subscribers.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="initialValue">The initial value before any emissions.</param>
    /// <returns>A shared observable with replay of the latest value.</returns>
    public static IObservable<T> ShareLatest<T>(
        this IObservable<T> source,
        T initialValue)
    {
        return source
            .StartWith(initialValue)
            .Replay(1)
            .RefCount();
    }

    /// <summary>
    /// Accumulates values into a running collection.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source observable.</param>
    /// <param name="maxSize">Maximum size of the accumulation.</param>
    /// <returns>An observable of accumulated collections.</returns>
    public static IObservable<IReadOnlyList<T>> Accumulate<T>(
        this IObservable<T> source,
        int? maxSize = null)
    {
        return source.Scan(
            new List<T>(),
            (acc, item) =>
            {
                List<T> list = [..acc, item];
                if (maxSize.HasValue && list.Count > maxSize.Value)
                {
                    list.RemoveAt(0);
                }

                return list;
            })
            .Select(list => (IReadOnlyList<T>)list.ToList());
    }
}
