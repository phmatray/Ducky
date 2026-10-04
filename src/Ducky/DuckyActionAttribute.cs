namespace Ducky;

/// <summary>
/// Marks an action record for the generator, which emits a dispatch helper for it (GEN-01). The runtime never reads it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class DuckyActionAttribute : Attribute
{
    /// <summary>Gets the name of the generated helper method; null uses the action's type name.</summary>
    public string? MethodName { get; init; }
}
