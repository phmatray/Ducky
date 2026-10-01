using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Client;

// S-7: the lifetime of AuthenticationStateProvider in this app and in the registrations of the WASM templates.
public static class Lifetimes
{
    public static string Describe()
    {
        var template = new ServiceCollection();
        template.AddAuthenticationStateDeserialization();
        var oidc = new ServiceCollection();
        oidc.AddOidcAuthentication(o =>
        {
            o.ProviderOptions.Authority = "https://login.example";
            o.ProviderOptions.ClientId = "spike";
        });
        var msal = new ServiceCollection();
        msal.AddMsalAuthentication(o => o.ProviderOptions.Authentication.ClientId = "spike");
        return string.Join('\n',
            $"this app: AuthenticationStateProvider {SpikeInfo.Lifetime(typeof(AuthenticationStateProvider))}",
            $"AddAuthenticationStateDeserialization (Web App template): {SpikeInfo.Lifetime(typeof(AuthenticationStateProvider), template)}",
            $"AddOidcAuthentication (RemoteAuthenticationService): {SpikeInfo.Lifetime(typeof(AuthenticationStateProvider), oidc)}",
            $"AddMsalAuthentication: {SpikeInfo.Lifetime(typeof(AuthenticationStateProvider), msal)}");
    }
}
