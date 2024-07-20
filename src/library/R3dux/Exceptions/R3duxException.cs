// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Exceptions;

/// <summary>
/// Exception type for the R3dux library.
/// </summary>
public class R3duxException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="R3dux.Exceptions.R3duxException"/> class.
    /// </summary>
    public R3duxException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="R3dux.Exceptions.R3duxException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public R3duxException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="R3dux.Exceptions.R3duxException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public R3duxException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
