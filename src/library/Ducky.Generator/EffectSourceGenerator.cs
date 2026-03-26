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
/// A source generator that creates AsyncEffect subclasses from static partial classes
/// decorated with [DuckyEffect]. Each public static Task-returning method becomes
/// a generated AsyncEffect&lt;TAction&gt; class.
/// </summary>
[Generator]
public class EffectSourceGenerator : SourceGeneratorBase
{
    private const string EffectAttributeFqn =
        "Ducky.DuckyEffectAttribute";

    private const string AsyncEffectBaseType =
        "Ducky.Middlewares.AsyncEffect.AsyncEffect";

    /// <inheritdoc/>
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classesProvider =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => IsCandidateClass(node),
                    static (ctx, _) => GetClassIfDuckyEffect(ctx))
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

        foreach (AttributeListSyntax attributeList in classDecl.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string name = attribute.Name.ToString();
                if (name is "DuckyEffect"
                    or "DuckyEffectAttribute"
                    or "Ducky.DuckyEffect"
                    or "Ducky.DuckyEffectAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ClassDeclarationSyntax? GetClassIfDuckyEffect(
        in GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        foreach (AttributeListSyntax attributeList in classDecl.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                SymbolInfo symbolInfo =
                    context.SemanticModel.GetSymbolInfo(attribute);
                ISymbol? targetSymbol =
                    symbolInfo.Symbol
                    ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (targetSymbol is IMethodSymbol attributeConstructor)
                {
                    string fullName =
                        attributeConstructor.ContainingType.ToDisplayString();
                    if (fullName == EffectAttributeFqn)
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
            SemanticModel semanticModel =
                compilation.GetSemanticModel(classSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classSyntax)
                is not { } classSymbol)
            {
                continue;
            }

            GenerateEffects(context, classSymbol);
        }
    }

    private void GenerateEffects(
        in SourceProductionContext context,
        INamedTypeSymbol classSymbol)
    {
        SymbolDisplayFormat fqf = SymbolDisplayFormat.FullyQualifiedFormat;
        string classFqn = classSymbol.ToDisplayString(fqf);
        string className = classSymbol.Name;

        string? ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method
                || !method.IsStatic
                || method.DeclaredAccessibility != Accessibility.Public
                || method.Parameters.Length == 0)
            {
                continue;
            }

            // Must return Task
            string returnType = method.ReturnType.ToDisplayString();
            if (returnType is not "System.Threading.Tasks.Task"
                and not "Task")
            {
                continue;
            }

            GenerateEffectClass(
                context, fqf, classFqn, className, ns, method);
        }
    }

    private void GenerateEffectClass(
        in SourceProductionContext context,
        SymbolDisplayFormat fqf,
        string classFqn,
        string className,
        string? ns,
        IMethodSymbol method)
    {
        string methodName = method.Name;
        string actionTypeFqn =
            method.Parameters[0].Type.ToDisplayString(fqf);

        // Classify parameters
        List<EffectParameterInfo> paramInfos =
            ClassifyParameters(method, fqf);

        List<EffectParameterInfo> diParams = paramInfos
            .Where(p => p.Kind == ParameterKind.DI)
            .ToList();

        // Build the generated class
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is auto-generated by Ducky.");
        sb.AppendLine();

        if (ns is not null)
        {
            sb.AppendLine("namespace " + ns + ";");
            sb.AppendLine();
        }

        string effectClassName =
            className + "_" + methodName + "_Effect";
        sb.AppendLine(
            "public sealed class " + effectClassName
            + " : " + AsyncEffectBaseType
            + "<" + actionTypeFqn + ">");
        sb.AppendLine("{");

        // DI fields and constructor
        if (diParams.Count > 0)
        {
            EmitDiFieldsAndConstructor(
                sb, effectClassName, diParams);
        }

        // HandleAsync override
        EmitHandleMethod(
            sb, actionTypeFqn, classFqn, methodName, paramInfos);

        sb.AppendLine("}");

        string hintName = effectClassName + ".g.cs";
        AddSource(context, hintName, sb.ToString());
    }

    private static List<EffectParameterInfo> ClassifyParameters(
        IMethodSymbol method,
        SymbolDisplayFormat fqf)
    {
        List<EffectParameterInfo> paramInfos = [];

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            IParameterSymbol param = method.Parameters[i];
            string paramTypeDisplay = param.Type.ToDisplayString();
            string paramTypeFqn = param.Type.ToDisplayString(fqf);

            ParameterKind kind;
            if (i == 0)
            {
                kind = ParameterKind.Action;
            }
            else if (paramTypeDisplay is "Ducky.IStateProvider"
                     or "IStateProvider")
            {
                kind = ParameterKind.StateProvider;
            }
            else if (paramTypeDisplay is "Ducky.IDispatcher"
                     or "IDispatcher")
            {
                kind = ParameterKind.Dispatcher;
            }
            else if (paramTypeDisplay
                     is "System.Threading.CancellationToken"
                     or "CancellationToken")
            {
                kind = ParameterKind.CancellationToken;
            }
            else
            {
                kind = ParameterKind.DI;
            }

            paramInfos.Add(new EffectParameterInfo
            {
                Kind = kind,
                TypeFqn = paramTypeFqn,
                Name = param.Name
            });
        }

        return paramInfos;
    }

    private static void EmitDiFieldsAndConstructor(
        StringBuilder sb,
        string effectClassName,
        List<EffectParameterInfo> diParams)
    {
        foreach (EffectParameterInfo di in diParams)
        {
            sb.AppendLine(
                "    private readonly "
                + di.TypeFqn + " _" + di.Name + ";");
        }

        sb.AppendLine();

        // Constructor
        sb.Append("    public " + effectClassName + "(");
        for (int i = 0; i < diParams.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(diParams[i].TypeFqn + " " + diParams[i].Name);
        }

        sb.AppendLine(")");
        sb.AppendLine("    {");
        foreach (EffectParameterInfo di in diParams)
        {
            sb.AppendLine(
                "        _" + di.Name + " = " + di.Name + ";");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    private static void EmitHandleMethod(
        StringBuilder sb,
        string actionTypeFqn,
        string classFqn,
        string methodName,
        List<EffectParameterInfo> paramInfos)
    {
        sb.AppendLine(
            "    public override async "
            + "System.Threading.Tasks.Task HandleAsync(");
        sb.AppendLine("        " + actionTypeFqn + " action,");
        sb.AppendLine("        Ducky.IStateProvider stateProvider,");
        sb.AppendLine(
            "        System.Threading.CancellationToken token)");
        sb.AppendLine("    {");

        // Build call arguments in original parameter order
        StringBuilder callArgs = new();
        for (int i = 0; i < paramInfos.Count; i++)
        {
            if (i > 0)
            {
                callArgs.Append(", ");
            }

            string arg = paramInfos[i].Kind switch
            {
                ParameterKind.Action => "action",
                ParameterKind.StateProvider => "stateProvider",
                ParameterKind.Dispatcher => "Dispatcher",
                ParameterKind.CancellationToken => "token",
                ParameterKind.DI => "_" + paramInfos[i].Name,
                _ => paramInfos[i].Name
            };

            callArgs.Append(arg);
        }

        sb.AppendLine(
            "        await " + classFqn + "." + methodName
            + "(" + callArgs + ");");
        sb.AppendLine("    }");
    }

    private enum ParameterKind
    {
        Action,
        StateProvider,
        Dispatcher,
        CancellationToken,
        DI
    }

    private class EffectParameterInfo
    {
        public ParameterKind Kind { get; set; }
        public string TypeFqn { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
