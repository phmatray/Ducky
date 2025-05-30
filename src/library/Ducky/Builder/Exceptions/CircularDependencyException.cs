namespace Ducky.Builder;

/// <summary>
/// Exception thrown when circular dependencies are detected.
/// </summary>
public class CircularDependencyException : InvalidOperationException
{
    /// <summary>
    /// Gets the chain of dependencies that form the circular reference.
    /// </summary>
    public List<Type> DependencyChain { get; }

    /// <summary>
    /// Initializes a new instance of the CircularDependencyException class.
    /// </summary>
    /// <param name="dependencyChain">The chain of dependencies forming the circular reference.</param>
    public CircularDependencyException(List<Type> dependencyChain)
        : base(CreateMessage(dependencyChain))
    {
        DependencyChain = dependencyChain;
    }

    /// <summary>
    /// Initializes a new instance of the CircularDependencyException class.
    /// </summary>
    public CircularDependencyException()
        : base()
    {
        DependencyChain = new List<Type>();
    }

    /// <summary>
    /// Initializes a new instance of the CircularDependencyException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CircularDependencyException(string message)
        : base(message)
    {
        DependencyChain = new List<Type>();
    }

    /// <summary>
    /// Initializes a new instance of the CircularDependencyException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CircularDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
        DependencyChain = new List<Type>();
    }

    private static string CreateMessage(List<Type> dependencyChain)
    {
        string chain = string.Join(" -> ", dependencyChain.Select(t => t.Name));

        return $$"""
                 Circular dependency detected in middleware chain:
                 {{chain}}

                 This creates an infinite loop that will prevent your application from starting.

                 To fix this:
                 1. Review the dependencies of each middleware
                 2. Remove or refactor the circular dependency
                 3. Consider using a different middleware architecture

                 Common causes:
                 - Middleware A depends on Service B, which depends on Middleware A
                 - Effects that dispatch actions handled by their own middleware
                 """;
    }
}
