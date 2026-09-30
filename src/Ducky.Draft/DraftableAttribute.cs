namespace Ducky.Draft;

/// <summary>Marks a partial record for which the Draft generator emits a nested <c>Draft</c> type and <c>Produce</c>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DraftableAttribute : Attribute;
