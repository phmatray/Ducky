namespace Ducky.CodeGen.Core;

public class ActionDispatcherGeneratorOptions
{
    public string ActionName { get; set; } = string.Empty;
    
    public string ActionFullyQualifiedName { get; set; } = string.Empty;
    
    public List<ParameterDescriptor> Parameters { get; set; } = new();
}