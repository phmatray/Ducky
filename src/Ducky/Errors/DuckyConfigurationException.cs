namespace Ducky;

/// <summary>
/// Thrown when Ducky is misconfigured. It lists every problem found, not just the first.
/// </summary>
public sealed class DuckyConfigurationException : InvalidOperationException
{
    internal DuckyConfigurationException(IReadOnlyList<DuckyError> errors, Exception? innerException = null)
        : base(string.Join(Environment.NewLine, errors.Select(DuckyErrors.Format)), innerException) => Errors = [.. errors];

    /// <summary>Gets every problem found.</summary>
    public IReadOnlyList<DuckyError> Errors { get; }
}
