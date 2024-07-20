// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace R3dux.Tests.TestModels;

/// <summary>
/// Represents a sample entity with an Guid identifier.
/// </summary>
internal sealed record SampleStringEntity
    : IEntity<string>
{
    public SampleStringEntity()
    {
    }

    public SampleStringEntity(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
