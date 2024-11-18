// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Extensions.Selectors;

/// <summary>
/// Provides memoization functionalities for selectors to minimize re-computations
/// and allow for efficient state management.
/// </summary>
public static class MemoizedSelector
{
    /// <summary>
    /// Creates a memoized selector that caches the results of the selector function based on the provided dependencies.
    /// </summary>
    /// <typeparam name="TState">The type of the state.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">The selector function to be memoized.</param>
    /// <param name="dependencies">The functions representing dependencies that the selector relies on.</param>
    /// <returns>A memoized selector function.</returns>
    public static Func<TState, TResult> Create<TState, TResult>(
        Func<TState, TResult> selector,
        params Func<TState, object>[] dependencies)
        where TState : notnull
    {
        var cache = new Dictionary<TState, (TResult Result, object[] Dependencies)>();

        return state =>
        {
            var currentDependencies = Array.ConvertAll(dependencies, dep => dep(state));

            if (cache.TryGetValue(state, out var cacheEntry) &&
                AreDependenciesEqual(cacheEntry.Dependencies, currentDependencies))
            {
                return cacheEntry.Result;
            }

            var result = selector(state);
            cache[state] = (result, currentDependencies);
            return result;
        };
    }

    /// <summary>
    /// Composes two selector functions into one, allowing for efficient state transformations.
    /// </summary>
    /// <typeparam name="TState">The type of the state.</typeparam>
    /// <typeparam name="TIntermediate">The type of the intermediate result.</typeparam>
    /// <typeparam name="TResult">The type of the final result.</typeparam>
    /// <param name="intermediateSelector">The intermediate selector function.</param>
    /// <param name="finalSelector">The final selector function.</param>
    /// <param name="dependencies">The functions representing dependencies that the selectors rely on.</param>
    /// <returns>A memoized selector function that composes the intermediate and final selectors.</returns>
    public static Func<TState, TResult> Compose<TState, TIntermediate, TResult>(
        Func<TState, TIntermediate> intermediateSelector,
        Func<TIntermediate, TResult> finalSelector,
        params Func<TState, object>[] dependencies)
        where TState : notnull
    {
        var memoizedIntermediate = Create(intermediateSelector, dependencies);
        return Create(state => finalSelector(memoizedIntermediate(state)), dependencies);
    }

    /// <summary>
    /// Compares two sets of dependencies to determine if they are equal.
    /// </summary>
    /// <param name="oldDeps">The old set of dependencies.</param>
    /// <param name="newDeps">The new set of dependencies.</param>
    /// <returns>True if the dependencies are equal; otherwise, false.</returns>
    private static bool AreDependenciesEqual(object[] oldDeps, object[] newDeps)
    {
        return oldDeps.Length == newDeps.Length
               && !oldDeps.Where((t, i) => !Equals(t, newDeps[i])).Any();
    }
}
