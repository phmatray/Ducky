using System.Collections.Generic;

namespace TheBlazorState.Generators;

internal sealed record InjectSubscriptionModel
{
    public string Namespace { get; init; } = null!;
    public string ClassName { get; init; } = null!;
    public List<string> SharedStatePropertyNames { get; init; } = null!;
    public bool UserImplementsDisposable { get; init; }
    public bool HasPersistProperties { get; init; }
}
