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
        Builder.Line("using System;");
        Builder.EmptyLine();
        Builder.Line("public static partial class ActionDispatcher");
        Builder.Braces(() =>
        {
            Builder.Summary($"Dispatches a new {methodName} action.");
            Builder.SummaryParam("dispatcher", "The dispatcher instance.");
            // Additional parameter summaries can be inserted here.

            Builder.Line($"public static void {methodName}(");
            Builder.Indent(() =>
            {
                // Append each parameter on its own indented line.
                for (int i = 0; i < parameterLines.Count; i++)
                {
                    string parameter = parameterLines[i].Replace("IDispatcher", "Ducky.IDispatcher");

                    // Last parameter: append closing parenthesis on the same line.
                    Builder.Line((i < parameterLines.Count - 1) ? $"{parameter}," : $"{parameter})");
                }
            });

            Builder.Braces(() =>
            {
                // Add braces for the null-check.
                Builder.Line("if (dispatcher is null)");
                Builder.Braces(() => Builder.Line("throw new System.ArgumentNullException(nameof(dispatcher));"));
                // Ensure the fully qualified record name uses a single global:: prefix.
                string typeName = (fullyQualifiedRecordName.StartsWith("global::"))
                    ? fullyQualifiedRecordName
                    : "global::" + fullyQualifiedRecordName;
                Builder.EmptyLine();
                Builder.Line($"dispatcher.Dispatch(new {typeName}({argumentList}));");
            });
        });
    }
}
