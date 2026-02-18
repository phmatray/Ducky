using System.Collections.Generic;

namespace Ducky.Generator.Descriptors;

/// <summary>
/// Describes an action method to generate in a component.
/// </summary>
internal class ComponentActionDescriptor
{
    public string ActionName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public List<ParameterDescriptor> Parameters { get; set; } = [];
}
