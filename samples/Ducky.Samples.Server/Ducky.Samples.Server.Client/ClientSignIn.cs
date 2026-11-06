using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ducky.Samples.Server.Client;

/// <summary>
/// A client-side sign-in for the /wasm-user document only, standing in for MSAL's RemoteAuthenticationService: Scoped, so
/// the renderer's instance is the one that raises <see cref="AuthenticationStateProvider.AuthenticationStateChanged"/>
/// (SPEC §11.6). Starts signed in as alice.
/// </summary>
public sealed class ClientSignIn : AuthenticationStateProvider
{
    private AuthenticationState _state = For("alice");

    /// <summary>Gets the signed-in user's NameIdentifier.</summary>
    public string User => _state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);

    /// <summary>Signs <paramref name="user"/> in and notifies the change.</summary>
    public void SignIn(string user)
    {
        _state = For(user);
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    private static AuthenticationState For(string user) =>
        new(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "client")));
}
