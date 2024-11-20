// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3;

namespace Ducky;

/// <summary>
/// Provides extension methods for observable sequences.
/// </summary>
public static class ObservableExtensions
{
    /// <summary>
    /// Returns the first element of an observable sequence synchronously.
    /// </summary>
    /// <param name="observable">The observable sequence to return the first element of.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <returns>The first element of the observable sequence.</returns>
    public static T FirstSync<T>(this Observable<T> observable)
    {
        Task<T> stateAsync = observable.FirstAsync();
        stateAsync.Wait();
        return stateAsync.Result;
    }
}
