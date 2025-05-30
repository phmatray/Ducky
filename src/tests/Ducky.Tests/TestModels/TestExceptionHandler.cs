// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Pipeline;

namespace Ducky.Tests.TestModels;

/// <summary>
/// Test implementation of exception handler for testing.
/// </summary>
public sealed class TestExceptionHandler : IExceptionHandler
{
    public List<ActionErrorEventArgs> ActionErrors { get; } = [];
    public List<EffectErrorEventArgs> EffectErrors { get; } = [];

    public bool ShouldHandleActionErrors { get; set; } = true;
    public bool ShouldHandleEffectErrors { get; set; } = true;
    public bool ShouldThrowOnHandle { get; set; }

    public bool HandleActionError(ActionErrorEventArgs eventArgs)
    {
        ActionErrors.Add(eventArgs);

        if (ShouldThrowOnHandle)
        {
            throw new InvalidOperationException("Test handler exception");
        }

        return ShouldHandleActionErrors;
    }

    public bool HandleEffectError(EffectErrorEventArgs eventArgs)
    {
        EffectErrors.Add(eventArgs);

        if (ShouldThrowOnHandle)
        {
            throw new InvalidOperationException("Test handler exception");
        }

        return ShouldHandleEffectErrors;
    }
}
