// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Movies;

public class MovieException : Exception
{
    public MovieException()
    {
    }

    public MovieException(string? message)
        : base(message)
    {
    }

    public MovieException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
