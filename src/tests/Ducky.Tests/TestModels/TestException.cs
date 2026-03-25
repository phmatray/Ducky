// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.TestModels;

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
