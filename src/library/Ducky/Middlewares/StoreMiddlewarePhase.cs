namespace Ducky.Middlewares;

/// <summary>
/// Represents the phase of middleware execution.
/// </summary>
public enum StoreMiddlewarePhase
{
    /// <summary>
    /// Middleware executes before the action is processed.
    /// </summary>
    Before,

    /// <summary>
    /// Middleware executes after the action is processed.
    /// </summary>
    After
}
