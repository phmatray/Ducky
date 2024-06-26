using BlazorAppRxStore.Components;
using BlazorAppRxStore.Services;
using BlazorAppRxStore.Store;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add RxStore
builder.Services.AddBlazorStore();

builder.Services.AddSingleton<ReducerManager<CounterState>>(provider =>
    new ReducerManager<CounterState>(
        provider.GetRequiredService<ActionsSubject>(),
        new CounterState(),
        new Dictionary<string, IActionReducer<CounterState>>
        {
            { nameof(CounterReducer), new CounterReducer() }
        },
        new CounterReducerFactory()));

builder.Services.AddSingleton<ReducerManager<TodoState>>(
    provider => new ReducerManager<TodoState>(
        provider.GetRequiredService<ActionsSubject>(),
        new TodoState(),
        new Dictionary<string, IActionReducer<TodoState>> 
        {
            { nameof(TodoReducer), new TodoReducer() }
        },
        new TodoReducerFactory()));

builder.Services.AddRxStore<MessageState, MessageReducer>();
builder.Services.AddRxStore<CounterState, CounterReducer>();
builder.Services.AddRxStore<TodoState, TodoReducer>();
builder.Services.AddRxStore<TimerState, TimerReducer>();
//
// // builder.Services.AddSingleton<MovieEffects>();
builder.Services.AddSingleton<MovieService>();
builder.Services.AddRxStore<MovieState, MovieReducer>();


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
