using Microsoft.Extensions.DependencyInjection;

namespace Bustand.Detection;

/// <summary>
/// Detects Blazor rendering mode at service registration time.
/// </summary>
public static class BlazorModeDetector
{
    /// <summary>
    /// Returns true if running in WebAssembly (browser), false for Server/SSR.
    /// </summary>
    public static bool IsWebAssembly => OperatingSystem.IsBrowser();

    /// <summary>
    /// Gets the recommended default store lifetime based on detected mode.
    /// - WebAssembly: Singleton (scoped = singleton in WASM anyway)
    /// - Server: Scoped (per-circuit isolation for safety)
    /// </summary>
    public static ServiceLifetime RecommendedStoreLifetime =>
        IsWebAssembly ? ServiceLifetime.Singleton : ServiceLifetime.Scoped;
}
