namespace Ducky.Testing;

/// <summary>Thrown by the Ducky.Testing assertions; independent of any test framework.</summary>
public sealed class DuckyAssertionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DuckyAssertionException"/> class.</summary>
    public DuckyAssertionException()
    {
    }

    internal DuckyAssertionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal DuckyAssertionException(string message)
        : base(message)
    {
    }
}
