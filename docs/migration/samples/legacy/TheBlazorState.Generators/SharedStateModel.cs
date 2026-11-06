using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TheBlazorState.Generators;

internal sealed record SharedStateModel
{
    public string Namespace { get; init; } = null!;
    public string ClassName { get; init; } = null!;
    public List<SharedPropertyModel> Properties { get; init; } = null!;
}

internal sealed record SharedPropertyModel
{
    public string PropertyName { get; init; } = null!;
    public string FullTypeName { get; init; } = null!;
    public Location PropertyLocation { get; init; } = null!;
}
