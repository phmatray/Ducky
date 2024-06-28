using Demo.AppStore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add RxStore
services.AddR3dux();

builder.Services.AddScoped(_ => new Store<LayoutState>(
    initialState: new LayoutState(),
    reducer: new LayoutReducer(),
    effects: []
));

builder.Services.AddScoped(_ => new Store<int>(
    initialState: 10,
    reducer: new CounterReducer(),
    effects: [new IncrementEffect()]
));

builder.Services.AddScoped(_ => new Store<MessageState>(
    initialState: new MessageState(),
    reducer: new MessageReducer(),
    effects: []
));

services.AddScoped<MoviesService>();
builder.Services.AddScoped(_ => new Store<MovieState>(
    initialState: new MovieState(),
    reducer: new MovieReducer(),
    effects: []
));

builder.Services.AddScoped(_ => new Store<TimerState>(
    initialState: new TimerState(),
    reducer: new TimerReducer(),
    effects: []
));

builder.Services.AddScoped(_ => new Store<TodoState>(
    initialState: new TodoState(),
    reducer: new TodoReducer(),
    effects: []
));

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
