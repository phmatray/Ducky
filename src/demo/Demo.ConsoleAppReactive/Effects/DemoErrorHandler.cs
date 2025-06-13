namespace Demo.ConsoleAppReactive.Effects;

public class DemoErrorHandler : IReactiveEffectErrorHandler
{
    private readonly ILogger<DemoErrorHandler> _logger;
    private readonly Dictionary<Type, int> _retryCount = [];

    public DemoErrorHandler(ILogger<DemoErrorHandler> logger)
    {
        _logger = logger;
    }

    public Task<ErrorHandlingResult> HandleErrorAsync(
        Exception error,
        Type effectType,
        object? action)
    {
        _logger.LogError(
            error,
            "Error in effect {EffectType} processing action {ActionType}",
            effectType.Name,
            action?.GetType().Name ?? "None");

        int currentRetryCount = _retryCount.GetValueOrDefault(effectType, 0);

        // Different strategies based on error type
        if (error is TimeoutException)
        {
            if (currentRetryCount < 3)
            {
                TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, currentRetryCount)); // Exponential backoff
                _retryCount[effectType] = currentRetryCount + 1;
                return Task.FromResult(ErrorHandlingResult.Retry(delay));
            }
            else
            {
                _retryCount[effectType] = 0;
                return Task.FromResult(ErrorHandlingResult.Continue());
            }
        }

        if (error is ArgumentException or InvalidOperationException)
        {
            // Don't retry on logical errors
            return Task.FromResult(ErrorHandlingResult.Continue());
        }

        // Default: continue processing
        return Task.FromResult(ErrorHandlingResult.Continue());
    }

    public void OnRetry(Type effectType, int retryCount, Exception lastError)
    {
        _logger.LogInformation(
            "Retrying effect {EffectType}, attempt {RetryCount}",
            effectType.Name,
            retryCount);
    }
}
