namespace Ducky.Blazor;

/// <summary>Dispatched when writing a persisted slice fails, once per failure streak.</summary>
/// <param name="SliceKey">The key of the slice whose write failed.</param>
/// <param name="ErrorType">The type name of the error.</param>
/// <param name="Message">The error message.</param>
public sealed record PersistenceFailed(string SliceKey, string ErrorType, string Message);
