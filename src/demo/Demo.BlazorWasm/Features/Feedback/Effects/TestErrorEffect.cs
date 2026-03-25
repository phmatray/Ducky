// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that simulates errors for testing the error handling system.
/// </summary>
public class TestErrorEffect : AsyncEffect<TestErrorAction>
{
    /// <inheritdoc />
    public override Task HandleAsync(TestErrorAction action, IStateProvider stateProvider, CancellationToken cancellationToken = default)
    {
        // Simulate an error by throwing an exception
        throw new ApplicationException(action.ErrorMessage);
    }
}
