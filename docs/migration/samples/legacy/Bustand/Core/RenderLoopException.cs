namespace Bustand.Core;

/// <summary>
/// Exception thrown when <see cref="ZustandStore{TState}.Set(System.Func{TState, TState})"/>
/// is called during a component render cycle, which would cause an infinite render loop.
/// </summary>
/// <remarks>
/// <para>
/// This exception indicates a programming error where state is being mutated during
/// the render phase of a Blazor component. This would cause the component to re-render
/// infinitely since each render triggers another state change.
/// </para>
/// <para>
/// <b>Common causes:</b>
/// <list type="bullet">
/// <item>Calling store action methods inside component markup</item>
/// <item>Calling Set() in OnAfterRender without a guard condition</item>
/// <item>Calling Set() in a property getter that is accessed during render</item>
/// </list>
/// </para>
/// <para>
/// <b>Solutions:</b>
/// <list type="bullet">
/// <item>Move state mutations to event handlers or lifecycle methods</item>
/// <item>Use OnAfterRenderAsync with a flag to ensure single execution</item>
/// <item>Trigger state changes from user interactions, not render logic</item>
/// </list>
/// </para>
/// </remarks>
public sealed class RenderLoopException : InvalidOperationException
{
    /// <summary>
    /// Gets the type of the store that threw the exception.
    /// </summary>
    public Type StoreType { get; }

    /// <summary>
    /// Creates a new <see cref="RenderLoopException"/> for the specified store type.
    /// </summary>
    /// <param name="storeType">The type of the store where the exception occurred.</param>
    public RenderLoopException(Type storeType)
        : base($"Cannot call Set() during component render in {storeType.Name}. This would cause an infinite render loop.")
    {
        StoreType = storeType ?? throw new ArgumentNullException(nameof(storeType));
    }

    /// <summary>
    /// Creates a new <see cref="RenderLoopException"/> with the specified message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="storeType">The type of the store where the exception occurred.</param>
    public RenderLoopException(string message, Type storeType)
        : base(message)
    {
        StoreType = storeType ?? throw new ArgumentNullException(nameof(storeType));
    }

    /// <summary>
    /// Creates a new <see cref="RenderLoopException"/> with the specified message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="storeType">The type of the store where the exception occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public RenderLoopException(string message, Type storeType, Exception innerException)
        : base(message, innerException)
    {
        StoreType = storeType ?? throw new ArgumentNullException(nameof(storeType));
    }
}
