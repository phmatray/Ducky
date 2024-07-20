// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.TestModels;

/// <summary>
/// Represents a sample normalized state for collections.
/// </summary>
internal sealed record SampleState
    : NormalizedState<Guid, SampleGuidEntity, SampleState>
{
    // No additional implementation needed for the tests
}
