// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Configuration options for async effect middleware.
/// </summary>
public class EffectOptions
{
    /// <summary>
    /// When true, re-throws effect exceptions after logging. Useful in development.
    /// Default: false.
    /// </summary>
    public bool ThrowOnEffectError { get; set; }
}
