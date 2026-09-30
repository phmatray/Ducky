using Microsoft.CodeAnalysis;

namespace Spike;

[Generator]
public sealed class FloorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("Floor.g.cs", "namespace Spike; internal static class Floor { public const string Generated = \"yes\"; }"));
}
