namespace Ducky;

/// <summary>
/// Hands a slice's prerendered state to the interactive render through Ducky.Blazor. The generator turns it into a builder
/// call (GEN-02); the runtime never reads it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PrerenderAttribute : Attribute;
