// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generator;

/// <summary>
/// A source generator that creates SliceReducers subclasses from static partial classes
/// decorated with [DuckyReducer]. Each static On() method becomes a registered reducer.
/// </summary>
[Generator]
public class ReducerSourceGenerator : SourceGeneratorBase
{
    private const string ReducerAttributeFqn = "Ducky.DuckyReducerAttribute";
    private const string InitialStateAttributeFqn = "Ducky.InitialStateAttribute";

    /// <inheritdoc/>
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Filter for static partial classes with attributes
        IncrementalValuesProvider<ClassDeclarationSyntax> classesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsCandidateClass(node),
                static (ctx, _) => GetClassIfDuckyReducer(ctx))
            .Where(result => result is not null)
            .Select((result, _) => result!);

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(classesProvider.Collect()),
            (spc, source) => GenerateCode(source.Left, source.Right, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax classDecl)
        {
            return false;
        }

        if (classDecl.AttributeLists.Count == 0)
        {
            return false;
        }

        // Must be static and partial
        var hasStatic = false;
        var hasPartial = false;
        foreach (SyntaxToken modifier in classDecl.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.StaticKeyword))
            {
                hasStatic = true;
            }

            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                hasPartial = true;
            }
        }

        if (!hasStatic || !hasPartial)
        {
            return false;
        }

        // Quick syntax check for attribute name
        foreach (AttributeListSyntax attributeList in classDecl.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string name = attribute.Name.ToString();
                if (name == "DuckyReducer"
                    || name == "DuckyReducerAttribute"
                    || name == "Ducky.DuckyReducer"
                    || name == "Ducky.DuckyReducerAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ClassDeclarationSyntax? GetClassIfDuckyReducer(in GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        foreach (AttributeListSyntax attributeList in classDecl.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(attribute);
                ISymbol? targetSymbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (targetSymbol is IMethodSymbol attributeConstructor)
                {
                    string fullName = attributeConstructor.ContainingType.ToDisplayString();
                    if (fullName == ReducerAttributeFqn)
                    {
                        return classDecl;
                    }
                }
            }
        }

        return null;
    }

    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<ClassDeclarationSyntax> classes,
        in SourceProductionContext context)
    {
        if (classes.IsEmpty)
        {
            return;
        }

        foreach (ClassDeclarationSyntax classSyntax in classes)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classSyntax) is not { } classSymbol)
            {
                continue;
            }

            GenerateSliceReducer(context, classSymbol);
        }
    }

    private void GenerateSliceReducer(
        in SourceProductionContext context,
        INamedTypeSymbol classSymbol)
    {
        SymbolDisplayFormat fqf = SymbolDisplayFormat.FullyQualifiedFormat;

        // Collect all public static On methods with exactly 2 parameters
        List<OnMethodInfo> onMethods = [];
        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member is IMethodSymbol method
                && method.IsStatic
                && method.DeclaredAccessibility == Accessibility.Public
                && method.Name == "On"
                && method.Parameters.Length == 2)
            {
                string stateType = method.Parameters[0].Type.ToDisplayString(fqf);
                string actionType = method.Parameters[1].Type.ToDisplayString(fqf);
                string returnType = method.ReturnType.ToDisplayString(fqf);

                onMethods.Add(new OnMethodInfo(stateType, actionType, returnType));
            }
        }

        if (onMethods.Count == 0)
        {
            return;
        }

        // Validate all On methods use the same state type
        string inferredStateType = onMethods[0].StateType;
        for (int i = 1; i < onMethods.Count; i++)
        {
            if (onMethods[i].StateType != inferredStateType)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "DUCKY001",
                        "Inconsistent state types in [DuckyReducer] class",
                        "All On() methods in '{0}' must use the same state type as first parameter. Found '{1}' and '{2}'.",
                        "Ducky.Generator",
                        DiagnosticSeverity.Error,
                        true),
                    classSymbol.Locations.FirstOrDefault(),
                    classSymbol.Name,
                    inferredStateType,
                    onMethods[i].StateType));
                return;
            }
        }

        // Find [InitialState] member
        string? initialStateExpr = null;
        string classFqn = classSymbol.ToDisplayString(fqf);

        foreach (ISymbol member in classSymbol.GetMembers())
        {
            var hasInitialStateAttr = false;
            foreach (AttributeData attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == InitialStateAttributeFqn)
                {
                    hasInitialStateAttr = true;
                    break;
                }
            }

            if (hasInitialStateAttr)
            {
                if (member is IFieldSymbol || member is IPropertySymbol)
                {
                    initialStateExpr = classFqn + "." + member.Name;
                    break;
                }
            }
        }

        if (initialStateExpr is null)
        {
            initialStateExpr = "new " + inferredStateType + "()";
        }

        // Generate class
        string className = classSymbol.Name;
        string? ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is auto-generated by Ducky.");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine("namespace " + ns + ";");
            sb.AppendLine();
        }

        sb.AppendLine("public sealed class " + className + "Slice : Ducky.SliceReducers<" + inferredStateType + ">");
        sb.AppendLine("{");
        sb.AppendLine("    public " + className + "Slice()");
        sb.AppendLine("    {");

        foreach (OnMethodInfo method in onMethods)
        {
            sb.AppendLine("        On<" + method.ActionType + ">((state, action) => " + classFqn + ".On(state, action));");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public override " + inferredStateType + " GetInitialState()");
        sb.AppendLine("    {");
        sb.AppendLine("        return " + initialStateExpr + ";");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        string hintName = className + "Slice.g.cs";
        AddSource(context, hintName, sb.ToString());
    }

    private class OnMethodInfo(string stateType, string actionType, string returnType)
    {
        public string StateType { get; } = stateType;
        public string ActionType { get; } = actionType;
        public string ReturnType { get; } = returnType;
    }
}
