// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.TestModels;

public class TestException : Exception
{
    private const string DefaultMessage = "An error occurred";

    public TestException()
        : base(DefaultMessage)
    {
    }

    public TestException(string? message)
        : base(message)
    {
    }

    public TestException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
