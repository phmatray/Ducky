using System.Security.Claims;
using Ducky.Samples.Server;
using Ducky.Samples.Server.Client;
using Ducky.Samples.Server.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHealthChecks();
// PersistStorage.Server over the real MemoryDistributedCache; NameIdentifier (the default scope) comes from the cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddDuckySample();
builder.Services.AddSingleton<ServerProductCatalog>();
builder.Services.AddSingleton<IProductCatalog>(sp => sp.GetRequiredService<ServerProductCatalog>());

var app = builder.Build();
app.UseAuthentication();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/api/products", (IProductCatalog catalog, CancellationToken cancellationToken) => catalog.GetAsync(cancellationToken));
app.MapGet("/api/products/loads", (ServerProductCatalog catalog) => catalog.Loads);
app.MapGet("/api/ping", () => "pong");
// A test login for the E2E specs, not an auth recipe: any user name signs in, through a GET.
app.MapGet("/login", async (HttpContext context, string user, string? returnUrl) =>
{
    Claim[] claims = [new(ClaimTypes.NameIdentifier, user), new(ClaimTypes.Name, user)];
    await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    return Results.LocalRedirect(returnUrl ?? "/");
});
app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.LocalRedirect("/");
});
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SampleRegistration).Assembly);
await app.RunAsync();
