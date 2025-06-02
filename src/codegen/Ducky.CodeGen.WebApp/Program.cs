using Ducky.CodeGen.Core;
using Ducky.CodeGen.WebApp.Components;
using Ducky.CodeGen.WebApp.Data;
using Ducky.CodeGen.WebApp.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Extensions;
using MudBlazor.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add database
builder.Services.AddDbContext<CodeGenDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
                      "Data Source=codegen.db"));

// Add services to the container.
builder.Services.AddScoped<ActionCreatorGenerator>();
builder.Services.AddScoped<ReducerGenerator>();
builder.Services.AddScoped<ProfilingGenerator>();
builder.Services.AddScoped<StateGenerator>();
builder.Services.AddScoped<EffectsGenerator>();
builder.Services.AddScoped<IAppStoreService, AppStoreService>();
builder.Services.AddScoped<IAppStoreCodeGenerator, AppStoreCodeGenerator>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddMudExtensions();

WebApplication app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CodeGenDbContext>();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.Use(MudExWebApp.MudExMiddleware);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
