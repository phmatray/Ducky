using System.Collections.Generic;

namespace Ducky.Generator.Descriptors;

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
