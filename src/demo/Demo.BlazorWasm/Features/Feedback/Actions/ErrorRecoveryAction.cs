// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.Features.Feedback.Actions;

/// <summary>
/// Action to retry a failed operation.
/// </summary>
[DuckyAction]
public record RetryFailedOperation(object OriginalAction, string? CorrelationId = null);

/// <summary>
/// Action to report an error to external logging service.
/// </summary>
[DuckyAction]
public record ReportError(Exception Exception, string? UserFeedback = null);
