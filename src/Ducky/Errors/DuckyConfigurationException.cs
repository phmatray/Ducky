namespace Ducky;

/// <summary>
/// Thrown when Ducky is misconfigured. It lists every problem found, not just the first.
/// </summary>
public sealed class DuckyConfigurationException : InvalidOperationException
{
    internal DuckyConfigurationException(IReadOnlyList<DuckyError> errors, Exception? innerException = null)
        : base(Report(errors, []), innerException) => Errors = [.. errors];

    // The first-resolution report (INV-31): the coded errors, then the user code that threw (a slice constructor or Key,
    // a rule), each named with what it threw. The thrown exceptions become the inner exception.
    internal DuckyConfigurationException(IReadOnlyList<DuckyError> errors, IReadOnlyList<(string Source, Exception Thrown)> failures)
        : base(
            Report(errors, failures),
            failures.Count switch
            {
                0 => null,
                1 => failures[0].Thrown,
                _ => new AggregateException(failures.Select(failure => failure.Thrown)),
            }) => Errors = [.. errors];

    /// <summary>Gets every problem found.</summary>
    public IReadOnlyList<DuckyError> Errors { get; }

    // One line per coded error, then one per failure.
    private static string Report(IReadOnlyList<DuckyError> errors, IReadOnlyList<(string Source, Exception Thrown)> failures) =>
        string.Join(
            Environment.NewLine,
            errors.Select(DuckyErrors.Format).Concat(failures.Select(failure =>
                $"{failure.Source} threw {failure.Thrown.GetType().FullName}: {failure.Thrown.Message} See the inner exception.")));
}
