// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.Website2.Components;
using Demo.Website2.Features.JsonColoring;
using Demo.Website2.Features.JsonColoring.Services;
using Ducky.Blazor;
using MudBlazor.Services;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;

builder.Host.UseSerilog((context, loggerConfig)
    => loggerConfig.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true);

// Add front services
services.AddMudServices(
    config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
        config.SnackbarConfiguration.ShowCloseIcon = true;
    });

services.AddScoped<IJsonColorizer, JsonColorizer>();

// Add data services
services.AddTransient<IMoviesService, MoviesService>();

// Add RxStore
services.AddDucky(builder.Configuration);

WebApplication app = builder.Build();

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
