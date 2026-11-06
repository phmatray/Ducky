using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TheBlazorState.Generators;

internal sealed record PersistComponentModel
{
    public string Namespace { get; init; } = null!;
    public string ClassName { get; init; } = null!;
    public List<PersistPropertyModel> Properties { get; init; } = null!;
    public bool UserImplementsDisposable { get; init; }
    public bool UserOverridesOnInitialized { get; init; }
    public bool UserOverridesOnInitializedAsync { get; init; }
}

internal sealed record PersistPropertyModel
{
    public string PropertyName { get; init; } = null!;
    public string FullTypeName { get; init; } = null!;
    public string? TimeToLive { get; init; }
    public string BaseKey { get; init; } = null!;
    public Location PropertyLocation { get; init; } = null!;
}

