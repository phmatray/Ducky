namespace Ducky.Blazor;

/// <summary>
/// Ducky.Blazor's options: one instance per <see cref="DuckyBuilder"/>, to which every
/// <see cref="DuckyBlazorBuilderExtensions.AddBlazor"/> configure delegate is applied in call order.
/// </summary>
public sealed class BlazorOptions
{
    /// <summary>The first segment of every storage key, <c>{prefix}:{scope?}:{sliceKey}</c>. Default <c>"ducky"</c>.</summary>
    public string KeyPrefix { get; set; } = "ducky";

    /// <summary>
    /// The user or tenant segment of the storage key, <c>{prefix}:{scope?}:{sliceKey}</c> (SPEC §11.6). Default
    /// <see langword="null"/>. Server-storage keys use <c>Scope ?? DuckyScopes.NameIdentifier</c>. Local and Session keys
    /// get a scope segment only when <see cref="Scope"/> is set explicitly: the default never applies to them, so an app that
    /// never sets it keeps unscoped browser keys. A delegate that returns <see langword="null"/> means no reads and no writes
    /// for the scoped keys, never a fallback to a shared key. A browser app with a <see cref="Scope"/> and no Ducky
    /// components must render <c>&lt;DuckyInitializer&gt;</c>, or scoped hydration fails at
    /// <see cref="HydrationTimeout"/>.
    /// </summary>
    public Func<IServiceProvider, CancellationToken, ValueTask<string?>>? Scope { get; set; }

    /// <summary>
    /// How long hydration may take before it fails and persistence resumes. Default 5 s. It must be shorter than
    /// <see cref="DuckyBuilder.InitTimeout"/>, so the persistence timeout fires first (DUCKY316).
    /// </summary>
    public TimeSpan HydrationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// In the browser only, how long init waits for the prerender seed when the first component has not registered yet
    /// (a preload in <c>Program.cs</c> started init before <c>RunAsync</c>). Default 250 ms. It must be shorter than
    /// <see cref="HydrationTimeout"/> (DUCKY316).
    /// </summary>
    public TimeSpan PrerenderSeedWaitTimeout { get; set; } = TimeSpan.FromMilliseconds(250);

    // §5.1: read once, here, and read back by every browser-dependent branch when it runs. Settable so the package's
    // tests can set it inside configure.
#pragma warning disable RS0030 // justification: the single IsBrowser read of this package (§5.1)
    internal bool IsBrowser { get; set; } = OperatingSystem.IsBrowser();
#pragma warning restore RS0030
}
