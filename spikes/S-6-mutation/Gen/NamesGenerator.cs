using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Gen;

/// <summary>Emits <c>{Enum}Names.Of(value)</c> for every enum marked <c>[Names]</c>, skipping obsolete members.</summary>
[Generator]
public sealed class NamesGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var enums = context.SyntaxProvider.ForAttributeWithMetadataName(
            "NamesAttribute",
            static (node, _) => node is EnumDeclarationSyntax,
            static (ctx, _) => Describe((INamedTypeSymbol)ctx.TargetSymbol));
        context.RegisterSourceOutput(enums, static (spc, e) => spc.AddSource($"{e.Name}Names.g.cs", Emit(e)));
    }

    private static EnumInfo Describe(INamedTypeSymbol symbol) => new(
        symbol.Name,
        symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
        symbol.GetMembers().OfType<IFieldSymbol>()
            .Where(f => f.IsConst && !f.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute"))
            .Select(f => f.Name)
            .ToArray());

    internal static string Emit(EnumInfo e)
    {
        var sb = new StringBuilder();
        if (e.Namespace is not null)
        {
            sb.Append("namespace ").Append(e.Namespace).AppendLine(";");
        }
        sb.Append("public static class ").Append(e.Name).AppendLine("Names")
          .AppendLine("{")
          .Append("    public static string Of(").Append(e.Name).AppendLine(" value) => value switch")
          .AppendLine("    {");
        foreach (var member in e.Members)
        {
            sb.Append("        ").Append(e.Name).Append('.').Append(member).Append(" => \"").Append(member).AppendLine("\",");
        }
        sb.AppendLine("        _ => value.ToString(),")
          .AppendLine("    };")
          .AppendLine("}");
        return sb.ToString();
    }
}

/// <summary>The generator's equatable model.</summary>
internal sealed class EnumInfo(string name, string? ns, string[] members) : IEquatable<EnumInfo>
{
    public string Name => name;

    public string? Namespace => ns;

    public string[] Members => members;

    public bool Equals(EnumInfo? other) =>
        other is not null && Name == other.Name && Namespace == other.Namespace && Members.SequenceEqual(other.Members);

    public override bool Equals(object? obj) => Equals(obj as EnumInfo);

    public override int GetHashCode() => Name.GetHashCode();
}
