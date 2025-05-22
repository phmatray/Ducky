namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Dispatched when the policy ultimately fails or the circuit is open.
/// </summary>
public record ServiceUnavailableAction(string Reason, object OriginalAction);
