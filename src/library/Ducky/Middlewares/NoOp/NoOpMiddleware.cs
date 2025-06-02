using Ducky.Pipeline;

namespace Ducky.Middlewares.NoOp;

/// <summary>
/// A no-op middleware that simply passes through all actions unchanged.
/// Useful for testing, benchmarking, or as a pipeline placeholder.
/// </summary>
public sealed class NoOpMiddleware : MiddlewareBase;
