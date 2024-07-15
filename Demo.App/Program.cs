using Demo.App.Components;
using Demo.App.Helpers;
using Demo.AppStore;
using MudBlazor.Services;
using R3dux;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add front services
services.AddMudServices();
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