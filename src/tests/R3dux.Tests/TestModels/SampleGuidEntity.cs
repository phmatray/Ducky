// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.TestModels;

/// <summary>
/// Represents a sample entity with an Guid identifier.
/// </summary>
internal sealed record SampleGuidEntity
    : IEntity<Guid>
{
    public SampleGuidEntity()
    {
    }

    public SampleGuidEntity(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
