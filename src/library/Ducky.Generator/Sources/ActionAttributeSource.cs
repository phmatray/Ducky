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
        Builder.Line($"namespace {generatorNamespace}");
        Builder.Braces(() =>
        {
            Builder.Summary("An attribute that marks a record as an action that can be dispatched.");
            Builder.Line("[System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]");
            Builder.Line($"public class {attributeName} : System.Attribute");
            Builder.Braces(() =>
            {
                // Optionally, insert members or leave the body empty.
            });
        });
    }
}
