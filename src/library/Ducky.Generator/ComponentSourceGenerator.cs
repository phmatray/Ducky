using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generator.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generator;

/// <summary>
/// A source generator that creates Blazor component base classes for state slices marked with the [DuckyComponent] attribute.
/// Each generated component provides strongly-typed access to its state slice and action dispatch methods.
/// </summary>
[Generator]
public class ComponentSourceGenerator : SourceGeneratorBase
{
    private const string GeneratorNamespace = "Ducky.Generators";
    private const string AttributeName = "DuckyComponentAttribute";

    /// <inheritdoc/>
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Inject the marker attribute into the user's compilation.
        context.RegisterPostInitializationOutput(ctx =>
        {
            ComponentAttributeSource attributeSource = new(GeneratorNamespace, AttributeName);
            ctx.AddSource($"{AttributeName}.g.cs", attributeSource.ToSourceText());
        });

        // Create a syntax provider that filters for class declarations with any attributes.
        IncrementalValuesProvider<ClassDeclarationSyntax> classesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) =>
                    node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetClassDeclarationForSourceGen(ctx))
            .Where(result => result.componentAttributeFound)
            .Select((result, _) => result.classDeclaration);

        // Combine the overall compilation with the collected class declarations.
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(classesProvider.Collect()),
            (spc, source) => GenerateCode(source.Left, source.Right, spc));
    }

    private static (ClassDeclarationSyntax classDeclaration, bool componentAttributeFound)
        GetClassDeclarationForSourceGen(in GeneratorSyntaxContext context)
    {
        // Handle both regular classes and record classes
        var typeDeclaration = context.Node as TypeDeclarationSyntax;
        if (typeDeclaration is null)
        {
            return (default(ClassDeclarationSyntax)!, false);
        }

        var found = false;
        foreach (AttributeListSyntax attributeList in typeDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol methodSymbol)
                {
                    string attributeFullName = methodSymbol.ContainingType.ToDisplayString();
                    if (attributeFullName == $"{GeneratorNamespace}.{AttributeName}"
                        || attributeFullName.EndsWith("." + AttributeName))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                break;
            }
        }

        // Return the original node cast as ClassDeclarationSyntax if it's a class, 
        // or null if it's a record (we'll handle records separately if needed)
        if (context.Node is ClassDeclarationSyntax classDeclaration)
        {
            return (classDeclaration, found);
        }
        
        // For now, we don't support records with [DuckyComponent]
        return (default(ClassDeclarationSyntax)!, false);
    }

    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<ClassDeclarationSyntax> classes,
        in SourceProductionContext context)
    {
        foreach (ClassDeclarationSyntax classSyntax in classes)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(classSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
            {
                continue;
            }

            // Extract component configuration from the attribute
            (ComponentDescriptor descriptor, string rootStateType)? componentInfo =
                ExtractComponentDescriptorFromSymbol(semanticModel, classSyntax, classSymbol);
            if (componentInfo is null)
            {
                continue;
            }

            // Extract component info
            string namespaceName = classSymbol.ContainingNamespace?.ToDisplayString() ?? "Generated";
            string rootStateType = componentInfo.Value.rootStateType;
            ComponentDescriptor descriptor = componentInfo.Value.descriptor;

            // Generate the component code
            // NOTE: Cannot use ComponentGenerator from Ducky.CodeGen.Core here because
            // source generators cannot reference assemblies that use Microsoft.CodeAnalysis.Workspaces
            string generatedCode = GenerateComponentCode(
                namespaceName,
                descriptor.ComponentName,
                rootStateType,
                descriptor.StateSliceType,
                descriptor.StateSliceProperty,
                descriptor.Actions);

            string hintName = $"{componentInfo.Value.descriptor.ComponentName}.g.cs";
            AddSource(context, hintName, generatedCode);
        }
    }

    private static (ComponentDescriptor descriptor, string rootStateType)? ExtractComponentDescriptorFromSymbol(
        SemanticModel semanticModel,
        ClassDeclarationSyntax classSyntax,
        INamedTypeSymbol classSymbol)
    {
        foreach (AttributeListSyntax attributeList in classSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (semanticModel.GetSymbolInfo(attribute).Symbol is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                string attributeFullName = methodSymbol.ContainingType.ToDisplayString();
                if (attributeFullName != $"{GeneratorNamespace}.{AttributeName}"
                    && !attributeFullName.EndsWith("." + AttributeName))
                {
                    continue;
                }

                // Extract attribute properties
                string componentName =
                    GetAttributeProperty(attribute, "ComponentName") ?? $"{classSymbol.Name}Component";
                string rootStateType = GetAttributeProperty(attribute, "RootStateType") ?? "AppState";
                string stateSliceProperty = GetAttributeProperty(attribute, "StateSliceProperty") ?? classSymbol.Name;

                // Extract actions (placeholder for now)
                List<ComponentActionDescriptor> actions = ExtractActionsFromStateSlice(classSymbol);

                ComponentDescriptor descriptor = new()
                {
                    ComponentName = componentName,
                    StateSliceName = classSymbol.Name,
                    StateSliceType = classSymbol.Name,
                    StateSliceProperty = stateSliceProperty,
                    Actions = actions
                };

                return (descriptor, rootStateType);
            }
        }

        return null;
    }

    private static string? GetAttributeProperty(AttributeSyntax attribute, string propertyName)
    {
        if (attribute.ArgumentList?.Arguments is not { } arguments)
        {
            return null;
        }

        foreach (AttributeArgumentSyntax argument in arguments)
        {
            if (argument.NameEquals?.Name.Identifier.Text == propertyName
                && argument.Expression is LiteralExpressionSyntax literal)
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    private static List<ComponentActionDescriptor> ExtractActionsFromStateSlice(
        INamedTypeSymbol stateSliceSymbol)
    {
        // For now, we'll return an empty list - actions would typically be discovered through
        // other means like analyzing the reducers or effects associated with this state slice
        // This is a placeholder for future enhancement
        _ = stateSliceSymbol; // Suppress unused parameter warning

        return new List<ComponentActionDescriptor>();
    }

    private static string GenerateComponentCode(
        string namespaceName,
        string componentName,
        string rootStateType,
        string stateSliceType,
        string stateSliceProperty,
        List<ComponentActionDescriptor> actions)
    {
        System.Text.StringBuilder sb = new();

        // Using directives
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Microsoft.AspNetCore.Components;");
        sb.AppendLine("using Ducky;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {namespaceName}");
        sb.AppendLine("{");

        // Class declaration
        sb.AppendLine($"    public abstract class {componentName} : ComponentBase, IDisposable");
        sb.AppendLine("    {");

        // Store property with [Inject] attribute
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// The injected store instance.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [Inject]");
        sb.AppendLine($"        protected IStore<{rootStateType}> Store {{ get; set; }} = default!;");
        sb.AppendLine();

        // State property
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// The current {stateSliceType} slice.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        protected {stateSliceType} State => Store.GetState().{stateSliceProperty};");
        sb.AppendLine();

        // OnInitialized method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Component initialization - subscribes to state changes.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        protected override void OnInitialized()");
        sb.AppendLine("        {");
        sb.AppendLine("            Store.StateChanged += OnStateChanged;");
        sb.AppendLine("            base.OnInitialized();");
        sb.AppendLine("        }");
        sb.AppendLine();

        // OnStateChanged method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Called whenever {rootStateType} changes; triggers UI refresh.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        private void OnStateChanged({rootStateType} appState)");
        sb.AppendLine("        {");
        sb.AppendLine("            InvokeAsync(StateHasChanged);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Dispatch method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Dispatch any action.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        protected void Dispatch(object action)");
        sb.AppendLine("        {");
        sb.AppendLine("            Store.Dispatch(action);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate action methods
        foreach (ComponentActionDescriptor action in actions)
        {
            GenerateActionMethod(sb, action);
            sb.AppendLine();
        }

        // Dispose method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Unsubscribes from state changes when component is disposed.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public void Dispose()");
        sb.AppendLine("        {");
        sb.AppendLine("            Store.StateChanged -= OnStateChanged;");
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateActionMethod(System.Text.StringBuilder sb, ComponentActionDescriptor action)
    {
        // XML documentation
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Dispatches a {action.ActionName} action.");
        sb.AppendLine("        /// </summary>");
        foreach (ParameterDescriptor parameter in action.Parameters)
        {
            sb.AppendLine(
                $"        /// <param name=\"{parameter.ParamName}\">The {parameter.ParamName} parameter.</param>");
        }

        // Method signature
        sb.Append($"        protected void {action.ActionName}(");

        List<ParameterDescriptor> parametersList = action.Parameters.ToList();
        if (parametersList.Count > 0)
        {
            sb.AppendLine();
            for (int i = 0; i < parametersList.Count; i++)
            {
                ParameterDescriptor parameter = parametersList[i];
                string separator = i < parametersList.Count - 1 ? "," : ")";
                sb.AppendLine($"            {parameter.ParamType} {parameter.ParamName}{separator}");
            }
        }
        else
        {
            sb.AppendLine(")");
        }

        // Method body
        sb.AppendLine("        {");

        if (parametersList.Count > 0)
        {
            string argumentList = string.Join(", ", parametersList.Select(p => p.ParamName));
            sb.AppendLine($"            Dispatch(new {action.ActionType}({argumentList}));");
        }
        else
        {
            sb.AppendLine($"            Dispatch(new {action.ActionType}());");
        }

        sb.AppendLine("        }");
    }
}

/// <summary>
/// Describes a component to generate.
/// </summary>
internal class ComponentDescriptor
{
    public string ComponentName { get; set; } = string.Empty;
    public string StateSliceName { get; set; } = string.Empty;
    public string StateSliceType { get; set; } = string.Empty;
    public string StateSliceProperty { get; set; } = string.Empty;
    public List<ComponentActionDescriptor> Actions { get; set; } = [];
}

/// <summary>
/// Describes an action method to generate in a component.
/// </summary>
internal class ComponentActionDescriptor
{
    public string ActionName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public List<ParameterDescriptor> Parameters { get; set; } = [];
}

/// <summary>
/// Parameter descriptor for code generation.
/// </summary>
internal class ParameterDescriptor
{
    public string ParamName { get; set; } = string.Empty;
    public string ParamType { get; set; } = "object";
    public string? DefaultValue { get; set; }
}
