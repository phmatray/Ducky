namespace Ducky.Middlewares;

/// <summary>
/// Indicates whether middleware should block the pipeline or run fire-and-forget.
/// </summary>
public enum StoreMiddlewareAsyncMode
{
    /// <summary>
    /// Block pipeline until async completes (default/recommended).
    /// </summary>
    Await,

    /// <summary>
    /// Do not block pipeline; execute async in background.
    /// </summary>
    FireAndForget
}
