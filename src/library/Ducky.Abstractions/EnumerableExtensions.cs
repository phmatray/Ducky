// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Converts the source <see cref="IEnumerable{T}"/> to a <see cref="ValueCollection{T}"/>.
    /// </summary>
    /// <param name="source">The source <see cref="IEnumerable{T}"/>.</param>
    /// <typeparam name="T">The type of the elements in the source <see cref="IEnumerable{T}"/>.</typeparam>
    /// <returns>A new <see cref="ValueCollection{T}"/> instance.</returns>
    public static ValueCollection<T> ToValueCollection<T>(this IEnumerable<T> source)
    {
        return new(source);
    }
}
