using System.Text;
using System.Text.Json;
using Ducky.Generator.WebApp.Models;

namespace Ducky.Generator.WebApp.Services;

public interface IAppStoreCodeGenerator
{
    List<GeneratedFile> GenerateAppStore(AppStore appStore);
}

public class AppStoreCodeGenerator : IAppStoreCodeGenerator
{
    public List<GeneratedFile> GenerateAppStore(AppStore appStore)
    {
        var files = new List<GeneratedFile>();

        foreach (var slice in appStore.StateSlices)
        {
            // Generate state class
            files.Add(GenerateStateClass(slice, appStore.Namespace));
            
            // Generate actions
            files.Add(GenerateActionsClass(slice, appStore.Namespace));
            
            // Generate reducers
            files.Add(GenerateReducersClass(slice, appStore.Namespace));
            
            // Generate effects if any
            if (slice.Effects.Any())
            {
                files.Add(GenerateEffectsClass(slice, appStore.Namespace));
            }
            
            // Generate complete duck file
            files.Add(GenerateDuckFile(slice, appStore.Namespace));
        }

        // Generate main app store configuration
        files.Add(GenerateAppStoreConfiguration(appStore));

        return files;
    }

    private GeneratedFile GenerateStateClass(StateSlice slice, string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine("using Ducky;");
        sb.AppendLine("using Ducky.Normalization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.AppStore.{slice.Name};");
        sb.AppendLine();
        
        // Parse state definition to generate proper state record
        var stateDefinition = JsonSerializer.Deserialize<Dictionary<string, object>>(slice.StateDefinition);
        sb.AppendLine($"public record {slice.Name}State(");
        
        var properties = new List<string>();
        foreach (var prop in stateDefinition!)
        {
            var propType = InferTypeFromValue(prop.Value);
            properties.Add($"    {propType} {prop.Key}");
        }
        
        sb.AppendLine(string.Join(",\n", properties));
        sb.AppendLine(") : IState;");

        return new GeneratedFile
        {
            FileName = $"{slice.Name}State.cs",
            FileType = "State",
            Content = sb.ToString()
        };
    }

    private GeneratedFile GenerateActionsClass(StateSlice slice, string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Ducky.FluxStandardActions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.AppStore.{slice.Name};");
        sb.AppendLine();
        
        foreach (var action in slice.Actions)
        {
            if (action.PayloadType == "void" || string.IsNullOrEmpty(action.PayloadType))
            {
                sb.AppendLine($"public record {action.Name};");
            }
            else
            {
                sb.AppendLine($"public record {action.Name}({action.PayloadType} Payload);");
            }
            sb.AppendLine();
        }

        return new GeneratedFile
        {
            FileName = $"{slice.Name}Actions.cs",
            FileType = "Actions",
            Content = sb.ToString()
        };
    }

    private GeneratedFile GenerateReducersClass(StateSlice slice, string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Ducky;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.AppStore.{slice.Name};");
        sb.AppendLine();
        sb.AppendLine($"public class {slice.Name}Reducers : SliceReducers<{slice.Name}State>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {slice.Name}Reducers() : base(GetInitialState())");
        sb.AppendLine("    {");
        
        foreach (var action in slice.Actions)
        {
            sb.AppendLine($"        On<{action.Name}>(Handle{action.Name});");
        }
        
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate initial state
        var stateDefinition = JsonSerializer.Deserialize<Dictionary<string, object>>(slice.StateDefinition);
        sb.AppendLine($"    private static {slice.Name}State GetInitialState()");
        sb.AppendLine("    {");
        sb.AppendLine($"        return new {slice.Name}State(");
        
        var initialValues = new List<string>();
        foreach (var prop in stateDefinition!)
        {
            var defaultValue = GetDefaultValue(prop.Value);
            initialValues.Add($"            {prop.Key}: {defaultValue}");
        }
        
        sb.AppendLine(string.Join(",\n", initialValues));
        sb.AppendLine("        );");
        sb.AppendLine("    }");
        sb.AppendLine();
        
        // Generate reducer methods
        foreach (var action in slice.Actions)
        {
            sb.AppendLine($"    private static {slice.Name}State Handle{action.Name}({slice.Name}State state, {action.Name} action)");
            sb.AppendLine("    {");
            sb.AppendLine("        // TODO: Implement reducer logic");
            sb.AppendLine("        return state;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");

        return new GeneratedFile
        {
            FileName = $"{slice.Name}Reducers.cs",
            FileType = "Reducers",
            Content = sb.ToString()
        };
    }

    private GeneratedFile GenerateEffectsClass(StateSlice slice, string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Ducky.Middlewares.AsyncEffect;");
        sb.AppendLine("using Ducky.Middlewares.ReactiveEffect;");
        sb.AppendLine("using R3;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.AppStore.{slice.Name};");
        sb.AppendLine();
        
        foreach (var effect in slice.Effects)
        {
            if (effect.ImplementationType == "AsyncEffect")
            {
                sb.AppendLine($"public class {effect.Name} : AsyncEffect");
                sb.AppendLine("{");
                sb.AppendLine($"    public override async Task<object?> Handle(object action, IRootState rootState, CancellationToken cancellationToken)");
                sb.AppendLine("    {");
                sb.AppendLine("        // TODO: Implement async effect logic");
                sb.AppendLine("        return null;");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            else if (effect.ImplementationType == "ReactiveEffect")
            {
                sb.AppendLine($"public class {effect.Name} : ReactiveEffect");
                sb.AppendLine("{");
                sb.AppendLine($"    public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)");
                sb.AppendLine("    {");
                var triggerActions = JsonSerializer.Deserialize<List<string>>(effect.TriggerActions);
                sb.AppendLine("        return actions");
                foreach (var triggerAction in triggerActions!)
                {
                    sb.AppendLine($"            .OfActionType<{triggerAction}>()");
                }
                sb.AppendLine("            .Select(action => {");
                sb.AppendLine("                // TODO: Implement reactive effect logic");
                sb.AppendLine("                return new object(); // Replace with actual action");
                sb.AppendLine("            });");
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            sb.AppendLine();
        }

        return new GeneratedFile
        {
            FileName = $"{slice.Name}Effects.cs",
            FileType = "Effects",
            Content = sb.ToString()
        };
    }

    private GeneratedFile GenerateDuckFile(StateSlice slice, string namespaceName)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Ducky;");
        sb.AppendLine();
        sb.AppendLine($"namespace {namespaceName}.AppStore.{slice.Name};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {slice.Description ?? $"Duck pattern implementation for {slice.Name}"}");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public static class {slice.Name}Duck");
        sb.AppendLine("{");
        sb.AppendLine($"    public static readonly SliceReducers<{slice.Name}State> Reducers = new {slice.Name}Reducers();");
        sb.AppendLine("}");

        return new GeneratedFile
        {
            FileName = $"{slice.Name}Duck.cs",
            FileType = "Duck",
            Content = sb.ToString()
        };
    }

    private GeneratedFile GenerateAppStoreConfiguration(AppStore appStore)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("using Ducky;");
        sb.AppendLine("using Ducky.Builder;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        foreach (var slice in appStore.StateSlices)
        {
            sb.AppendLine($"using {appStore.Namespace}.AppStore.{slice.Name};");
        }
        sb.AppendLine();
        sb.AppendLine($"namespace {appStore.Namespace}.AppStore;");
        sb.AppendLine();
        sb.AppendLine($"public static class {appStore.Name}StoreConfiguration");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddAppStore(this IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine("        return services.AddDuckyStore(builder =>");
        sb.AppendLine("        {");
        sb.AppendLine("            builder");
        sb.AppendLine("                .AddDefaultMiddlewares()");
        
        foreach (var slice in appStore.StateSlices)
        {
            sb.AppendLine($"                .AddSlice<{slice.Name}State>()");
            
            foreach (var effect in slice.Effects)
            {
                sb.AppendLine($"                .AddEffect<{effect.Name}>()");
            }
        }
        
        sb.AppendLine("                .ConfigureStore(options =>");
        sb.AppendLine("                {");
        sb.AppendLine($"                    options.AssemblyNames = [\"{appStore.Namespace}\"];");
        sb.AppendLine("                });");
        sb.AppendLine("        });");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new GeneratedFile
        {
            FileName = $"{appStore.Name}StoreConfiguration.cs",
            FileType = "Configuration",
            Content = sb.ToString()
        };
    }

    private string InferTypeFromValue(object value)
    {
        return value switch
        {
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => "string",
                JsonValueKind.Number => "int",
                JsonValueKind.True or JsonValueKind.False => "bool",
                JsonValueKind.Array => "ImmutableList<object>",
                JsonValueKind.Object => "object",
                _ => "object"
            },
            string => "string",
            int => "int",
            bool => "bool",
            _ => "object"
        };
    }

    private string GetDefaultValue(object value)
    {
        return value switch
        {
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => $"\"{element.GetString()}\"",
                JsonValueKind.Number => element.GetInt32().ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => "ImmutableList<object>.Empty",
                JsonValueKind.Object => "new()",
                _ => "default"
            },
            string s => $"\"{s}\"",
            int i => i.ToString(),
            bool b => b.ToString().ToLower(),
            _ => "default"
        };
    }
}