namespace Ducky.Blazor;

/// <summary>
/// Ducky.Blazor's options: one instance per <see cref="DuckyBuilder"/>, to which every
/// <see cref="DuckyBlazorBuilderExtensions.AddBlazor"/> configure delegate is applied in call order.
/// </summary>
public sealed class BlazorOptions
{
    /// <summary>The first segment of every storage key, <c>{prefix}:{scope?}:{sliceKey}</c>. Default <c>"ducky"</c>.</summary>
    public string KeyPrefix { get; set; } = "ducky";

    // §5.1: read once, here, and read back by every browser-dependent branch when it runs. Settable so the package's
    // tests can set it inside configure.
#pragma warning disable RS0030 // justification: the single IsBrowser read of this package (§5.1)
    internal bool IsBrowser { get; set; } = OperatingSystem.IsBrowser();
#pragma warning restore RS0030
}
