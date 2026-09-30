namespace Ducky;

/// <summary>
/// Overrides the action type name of an action record, for example <c>[ActionType("cart/Add")]</c>.
/// The one attribute the runtime reads, once per action type per store.
/// </summary>
/// <param name="name">The action type name.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ActionTypeAttribute(string name) : Attribute
{
    /// <summary>Gets the action type name.</summary>
    public string Name { get; } = name;
}
