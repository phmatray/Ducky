namespace Ducky.Builder;

/// <summary>
/// Exception thrown when effects are added without their required middleware.
/// </summary>
public class MissingMiddlewareException : InvalidOperationException
{
    /// <summary>
    /// Gets the type of effect that was being added.
    /// </summary>
    public Type EffectType { get; }

    /// <summary>
    /// Gets the type of middleware that is required.
    /// </summary>
    public Type RequiredMiddlewareType { get; }

    /// <summary>
    /// Initializes a new instance of the MissingMiddlewareException class.
    /// </summary>
    /// <param name="effectType">The type of effect being added.</param>
    /// <param name="requiredMiddlewareType">The type of required middleware.</param>
    /// <param name="suggestion">Suggestion for fixing the issue.</param>
    public MissingMiddlewareException(Type effectType, Type requiredMiddlewareType, string suggestion)
        : base(CreateMessage(effectType, requiredMiddlewareType, suggestion))
    {
        EffectType = effectType;
        RequiredMiddlewareType = requiredMiddlewareType;
    }

    /// <summary>
    /// Initializes a new instance of the MissingMiddlewareException class.
    /// </summary>
    public MissingMiddlewareException()
        : base()
    {
        EffectType = typeof(object);
        RequiredMiddlewareType = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance of the MissingMiddlewareException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MissingMiddlewareException(string message)
        : base(message)
    {
        EffectType = typeof(object);
        RequiredMiddlewareType = typeof(object);
    }

    /// <summary>
    /// Initializes a new instance of the MissingMiddlewareException class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MissingMiddlewareException(string message, Exception innerException)
        : base(message, innerException)
    {
        EffectType = typeof(object);
        RequiredMiddlewareType = typeof(object);
    }

    private static string CreateMessage(Type effectType, Type requiredMiddlewareType, string suggestion)
    {
        return $$"""
            Cannot add {{effectType.Name}} without {{requiredMiddlewareType.Name}}.
            
            The {{requiredMiddlewareType.Name}} is required to handle {{effectType.Name}}.
            
            To fix this issue:
            {{suggestion}}
            
            Example:
            services.AddDuckyStore(builder => builder
                .{{suggestion}}
                .AddEffect<{{effectType.Name}}>()
            );
            """;
    }
}
