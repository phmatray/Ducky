// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.TestModels;

/// <summary>
/// Represents a sample normalized state for collections with GUID key.
/// </summary>
internal sealed record SampleGuidState
    : NormalizedState<Guid, SampleGuidEntity, SampleGuidState>
{
    // No additional implementation needed for the tests
}
