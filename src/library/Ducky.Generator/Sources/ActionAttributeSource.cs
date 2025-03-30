using Ducky.Generator.Core;

namespace Ducky.Generator.Sources;

/// <summary>
/// Represents the generated source for the DuckyAction attribute.
/// </summary>
/// <param name="generatorNamespace">The namespace for the generated code.</param>
/// <param name="attributeName">The name of the attribute.</param>
public class ActionAttributeSource(
    string generatorNamespace,
    string attributeName)
    : GeneratedSource
{
    /// <inheritdoc/>
    protected override void Build()
    {
        // The auto-generated header is already appended by PreBuild.
        Builder.AppendLine($"namespace {generatorNamespace}");
        Builder.AppendLine("{");
        Builder.Indent();
        Builder.AppendLine("/// <summary>");
        Builder.AppendLine("/// An attribute that marks a record as an action that can be dispatched.");
        Builder.AppendLine("/// </summary>");
        Builder.AppendLine("[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]");
        Builder.AppendLine($"public class {attributeName} : System.Attribute");
        Builder.AppendLine("{");
        Builder.Indent();
        // Optionally, insert members or leave the body empty.
        Builder.Outdent();
        Builder.AppendLine("}");
        Builder.Outdent();
        Builder.AppendLine("}");
    }
}
