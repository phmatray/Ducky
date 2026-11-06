namespace Ducky.Blazor;

public static partial class DuckyBlazorBuilderExtensions
{
    /// <summary>
    /// Connects the store to the Redux DevTools browser extension (SPEC §11.8). Enabled when called: guard the call with
    /// the app's own <c>IsDevelopment()</c>. Adds Ducky.Blazor. Registration is idempotent; configuration composes: every
    /// <paramref name="configure"/> of every call is applied, in call order, to one <see cref="DevToolsOptions"/>.
    /// </summary>
    /// <remarks>
    /// Time travel (development only): while the extension shows an entry other than the latest, the store vetoes
    /// <see cref="Origin.Local"/> and <see cref="Origin.Effect"/> dispatches, late effect results included, which complete
    /// <see cref="DispatchResult.Vetoed"/>. Library traffic (hydration, cross-tab) and the extension's own restores still
    /// reduce. Jumping back to the latest entry, a reset, an import, or DevTools going inert releases the veto.
    /// </remarks>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Sets options; <see langword="null"/> only registers.</param>
    /// <returns><paramref name="builder"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DuckyBuilder UseDevTools(this DuckyBuilder builder, Action<DevToolsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var registration = Registration(builder);
        if (registration.DevTools is null)
        {
            registration.DevTools = new();
            builder.Use<DevToolsMiddleware>();
        }

        configure?.Invoke(registration.DevTools);
        return builder;
    }
}
