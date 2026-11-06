namespace TheBlazorState.Attributes;

/// <summary>
/// Marks a partial property in a state class as reactive across components.
/// Any component injecting the state class re-renders when this property changes.
/// Can be combined with PersistAttribute for shared + persisted state.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SharedAttribute : Attribute
{
}
