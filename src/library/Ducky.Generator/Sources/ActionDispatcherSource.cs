using System.Collections.Generic;
using Ducky.Generator.Core;

namespace Ducky.Generator.Sources;

/// <summary>
/// Represents the generated source for an ActionDispatcher extension method.
/// </summary>
/// <param name="methodName">The name of the method to be generated.</param>
/// <param name="fullyQualifiedRecordName">The fully qualified name of the record type.</param>
/// <param name="argumentList">The list of arguments to be passed to the record constructor.</param>
/// <param name="parameterLines">The lines representing the method parameters.</param>
public class ActionDispatcherSource(
    string methodName,
    string fullyQualifiedRecordName,
    string argumentList,
    IReadOnlyList<string> parameterLines)
    : GeneratedSource
{
    protected override void Build()
    {
        // Using directives.
        Builder.AppendLine("using System;");
        Builder.AppendLine("using Ducky;");
        Builder.AppendLine();
        Builder.AppendLine("public static partial class ActionDispatcher");
        Builder.AppendLine("{");
        Builder.Indent();
        Builder.AppendLine($"public static void {methodName}(");
        Builder.Indent();

        // Append each parameter on its own line.
        for (int i = 0; i < parameterLines.Count; i++)
        {
            string comma = (i < parameterLines.Count - 1) ? "," : string.Empty;
            Builder.AppendLine($"{parameterLines[i]}{comma}");
        }

        Builder.Outdent();
        Builder.AppendLine(")");
        Builder.AppendLine($"    => dispatcher.Dispatch(new {fullyQualifiedRecordName}({argumentList}));");
        Builder.Outdent();
        Builder.AppendLine("}");
    }
}
