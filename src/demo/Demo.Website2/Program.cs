using Demo.Website2.Components;
using Demo.Website2.Features.JsonColoring;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add front services
services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.ShowCloseIcon = true;
});

services.AddScoped<IJsonColorizer, JsonColorizer>();

// Add data services
services.AddTransient<IMoviesService, MoviesService>();

// Add RxStore
services.AddR3dux(builder.Configuration);
// ==== or ====
// services.AddR3dux(options =>
// {
//     options.Assemblies = [typeof(CounterSlice).Assembly];
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
