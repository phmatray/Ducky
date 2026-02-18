namespace Ducky.Generator.Descriptors;

/// <summary>
/// Parameter descriptor for code generation.
/// </summary>
internal class ParameterDescriptor
{
    public string ParamName { get; set; } = string.Empty;
    public string ParamType { get; set; } = "object";
    public string? DefaultValue { get; set; }
}
