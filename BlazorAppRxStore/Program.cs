using BlazorAppRxStore.Components;
using BlazorAppRxStore.Store;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add RxStore
builder.Services.AddRxStore<MessageState, MessageReducer>();
builder.Services.AddRxStore<CounterState, CounterReducer>();
builder.Services.AddRxStore<TodoState, TodoReducer>();
builder.Services.AddRxStore<TimerState, TimerReducer>();

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