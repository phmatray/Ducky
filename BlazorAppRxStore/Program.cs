using BlazorAppRxStore.Components;
using BlazorAppRxStore.Services;
using BlazorAppRxStore.Store;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add RxStore
services.AddBlazorStore();

services.AddRxStore<CounterState, CounterReducer, CounterReducerFactory>();
services.AddRxStore<TodoState, TodoReducer, TodoReducerFactory>();
services.AddRxStore<MessageState, MessageReducer, MessageReducerFactory>();
services.AddRxStore<TimerState, TimerReducer, TimerReducerFactory>();

// // builder.Services.AddSingleton<MovieEffects>();
services.AddTransient<MovieService>();
services.AddRxStore<MovieState, MovieReducer, MovieReducerFactory>();

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
