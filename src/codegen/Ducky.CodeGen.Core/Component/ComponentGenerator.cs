using System.Collections.Generic;
using System.Linq;

namespace Ducky.CodeGen.Core;

/// <summary>
/// Generates strongly-typed component base classes for specific state slices.
/// Creates ComponentBase-derived classes with state binding and action dispatch methods.
/// </summary>
public class ComponentGenerator : SourceGeneratorBase<ComponentGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(ComponentGeneratorOptions opts)
    {
        return new CompilationUnitElement
        {
            Usings = new List<string>
            {
                "System",
                "System.Collections.Generic",
                "Microsoft.AspNetCore.Components",
                "Ducky"
            },
            Namespaces = new List<NamespaceElement>
            {
                new NamespaceElement
                {
                    Name = opts.Namespace,
                    Classes = opts.Components.Select(component => BuildComponentClass(opts, component)).ToList()
                }
            }
        };
    }

    private static ClassElement BuildComponentClass(ComponentGeneratorOptions opts, ComponentDescriptor component)
    {
        var properties = new List<PropertyElement>
        {
            // Store property with [Inject] attribute
            new PropertyElement
            {
                Name = "Store",
                Type = $"IStore<{opts.RootStateType}>",
                Accessibility = "protected",
                HasGetter = true,
                HasSetter = true,
                Attributes = new List<string> { "Inject" },
                DefaultValue = new ExpressionElement { Code = "default!" }
            },
            
            // State property
            new PropertyElement
            {
                Name = "State",
                Type = component.StateSliceType,
                Accessibility = "protected",
                HasGetter = true,
                HasSetter = false,
                GetterBody = new ExpressionElement { Code = $"Store.GetState().{component.StateSliceProperty}" },
                XmlDocumentation = $"/// <summary>The current {component.StateSliceType} slice.</summary>"
            }
        };

        var methods = new List<MethodElement>
        {
            // OnInitialized method
            new MethodElement
            {
                Name = "OnInitialized",
                ReturnType = "void",
                MethodBody = new ExpressionElement
                {
                    Code = @"Store.StateChanged += OnStateChanged;
        base.OnInitialized();"
                }
            },
            
            // OnStateChanged method
            new MethodElement
            {
                Name = "OnStateChanged",
                ReturnType = "void",
                Parameters = new List<ParameterDescriptor>
                {
                    new ParameterDescriptor { ParamName = "appState", ParamType = opts.RootStateType }
                },
                ExpressionBody = new ExpressionElement { Code = "InvokeAsync(StateHasChanged)" },
                XmlDocumentation = $"/// <summary>Called whenever {opts.RootStateType} changes; triggers UI refresh.</summary>"
            },
            
            // Dispatch method
            new MethodElement
            {
                Name = "Dispatch",
                ReturnType = "void",
                Parameters = new List<ParameterDescriptor>
                {
                    new ParameterDescriptor { ParamName = "action", ParamType = "object" }
                },
                ExpressionBody = new ExpressionElement { Code = "Store.Dispatch(action)" },
                XmlDocumentation = "/// <summary>Dispatch any action.</summary>"
            },
            
            // Dispose method
            new MethodElement
            {
                Name = "Dispose",
                ReturnType = "void",
                Parameters = new List<ParameterDescriptor>(),
                ExpressionBody = new ExpressionElement { Code = "Store.StateChanged -= OnStateChanged" }
            }
        };

        // Add action methods
        methods.AddRange(component.Actions.Select(BuildActionMethod));

        return new ClassElement
        {
            Name = component.ComponentName,
            IsStatic = false,
            IsAbstract = true,
            BaseClass = "ComponentBase",
            Interfaces = new List<string> { "IDisposable" },
            Properties = properties,
            Methods = methods
        };
    }

    private static MethodElement BuildActionMethod(ComponentActionDescriptor action)
    {
        var argumentList = string.Join(", ", action.Parameters.Select(p => p.ParamName));
        var dispatchCall = action.Parameters.Any() 
            ? $"new {action.ActionType}({argumentList})"
            : $"new {action.ActionType}()";

        return new MethodElement
        {
            Name = action.ActionName,
            ReturnType = "void",
            Parameters = action.Parameters.ToList(),
            ExpressionBody = new ExpressionElement { Code = $"Dispatch({dispatchCall})" }
        };
    }
}