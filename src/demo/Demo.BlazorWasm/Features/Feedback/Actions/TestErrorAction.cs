// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Features.Feedback.Actions;

/// <summary>
/// Test action that is used to simulate errors in the error handling demo.
/// </summary>
[DuckyAction]
public record TestErrorAction(string ErrorMessage = "This is a simulated error from an action!");
