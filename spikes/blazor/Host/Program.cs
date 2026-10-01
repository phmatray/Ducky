using System.Collections.Concurrent;
using System.Security.Claims;
using Client;
using Host;
using Host.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Server.Circuits;

var builder = WebApplication.CreateBuilder(args);
var components = builder.Services.AddRazorComponents();
if (Modes.Server)
{
    components.AddInteractiveServerComponents(o => o.DetailedErrors = true);
}

if (Modes.Wasm)
{
    components.AddInteractiveWebAssemblyComponents().AddAuthenticationStateSerialization();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<SpikeStore>();
builder.Services.AddScoped<CircuitHandler, CircuitLog>();
SpikeInfo.Descriptors = [.. builder.Services];
var log = new ConcurrentQueue<string>();
SpikeInfo.Log = log.Enqueue;

var app = builder.Build();
SpikeInfo.Root = app.Services;
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/login", async (HttpContext context, string user) =>
{
    var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user), new Claim(ClaimTypes.Name, user)], "spike");
    await context.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Redirect("/s7");
});
app.MapGet("/spike-log", () => string.Join('\n', log));
var endpoints = app.MapRazorComponents<App>().AddAdditionalAssemblies(typeof(Routes).Assembly);
if (Modes.Server)
{
    endpoints.AddInteractiveServerRenderMode();
}

if (Modes.Wasm)
{
    endpoints.AddInteractiveWebAssemblyRenderMode();
}

app.Run();
