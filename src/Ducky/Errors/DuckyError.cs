namespace Ducky;

/// <summary>
/// One configuration or misuse problem, with its code (<c>DUCKY3xx</c>), what happened with the concrete types and keys,
/// the exact fix, and the link to its documentation page.
/// </summary>
/// <param name="Code">The diagnostic code, for example <c>DUCKY304</c>.</param>
/// <param name="Message">What happened, naming the concrete types and keys.</param>
/// <param name="Fix">The exact fix.</param>
/// <param name="HelpLink">The link to the code's documentation page.</param>
public sealed record DuckyError(string Code, string Message, string Fix, string HelpLink);
