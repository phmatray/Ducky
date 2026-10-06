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

    /// <summary>
    /// How long the prerender seed waits for the store to go idle (no effect running except <c>LongRunning</c> ones, and
    /// no Ducky.Reactive work). On timeout no seed is persisted, a Warning is logged once per store, and the interactive
    /// side loads again. Default 5 seconds.
    /// </summary>
    public TimeSpan PrerenderIdleTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The most bytes the prerender seed may add, by estimate, to the Interactive Server start-circuit message. A slice
    /// that would push the seed past it is left out (logged once per slice), and the interactive side loads it again. The
    /// page shares SignalR's 32 KB <c>MaximumReceiveMessageSize</c> with every other <c>PersistentComponentState</c> user,
    /// such as the template's authentication state. Default 20 KB; the same budget applies, conservatively, to WebAssembly.
    /// </summary>
    public int PrerenderSeedMaxWireBytes { get; set; } = 20 * 1024;

    /// <summary>
    /// The largest stored value, in UTF-8 bytes of its JSON encoding, that crosses from JS to .NET inline. Default 16 KiB,
    /// well below Blazor Server's 32 KB <c>MaximumReceiveMessageSize</c> (SPEC §11.5, D11).
    /// </summary>
    public int InlinePayloadBytes { get; set; } = 16 * 1024;

    /// <summary>
    /// The largest stored value, in UTF-8 bytes, read through <c>IJSStreamReference</c>. A larger one reads as not found,
    /// with a Warning, and the next write replaces it. Default 5 MiB (SPEC §11.5, D11).
    /// </summary>
    public int MaxPayloadBytes { get; set; } = 5 * 1024 * 1024;

    // §5.1: read once, here, and read back by every browser-dependent branch when it runs. Settable so the package's
    // tests can set it inside configure.
#pragma warning disable RS0030 // justification: the single IsBrowser read of this package (§5.1)
    internal bool IsBrowser { get; set; } = OperatingSystem.IsBrowser();
#pragma warning restore RS0030
}
