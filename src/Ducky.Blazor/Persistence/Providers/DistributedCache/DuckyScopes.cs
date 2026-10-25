using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>Ready-made <see cref="BlazorOptions.Scope"/> delegates (SPEC §11.6).</summary>
public static class DuckyScopes
{
    /// <summary>
    /// The authenticated user's <see cref="ClaimTypes.NameIdentifier"/>, read from the <see cref="AuthenticationStateProvider"/>;
    /// <see langword="null"/> (no reads and no writes of scoped keys) when the user has none, as an anonymous user. The
    /// default scope of <see cref="PersistStorage.Server"/> keys, which then require an
    /// <see cref="AuthenticationStateProvider"/> (DUCKY315). The value must be unique across the identity providers the app
    /// accepts.
    /// </summary>
    public static Func<IServiceProvider, CancellationToken, ValueTask<string?>> NameIdentifier { get; } = static async (services, _) =>
    {
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var state = await services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync().ConfigureAwait(false);
        return state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    };
}
