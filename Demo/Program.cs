using Demo.AppStore;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Add services to the container.
services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add RxStore
services.AddR3dux();
services.AddSingleton(sp =>
{
    var dispatcher = sp.GetRequiredService<IDispatcher>();
    var moviesService = new MoviesService();
    
    SliceCollection slices =
    [
        new Slice<LayoutState>
        {
            Key = "layout",
            InitialState = new LayoutState(),
            Reducers = new LayoutReducers()
        },
        
        new Slice<int>
        {
            Key = "counter",
            InitialState = 10,
            Reducers = new CounterReducers(),
            Effects = [new IncrementEffect()]
        },
        
        new Slice<MessageState>
        {
            Key = "message",
            InitialState = new MessageState(),
            Reducers = new MessageReducers()
        },
    
        new Slice<MovieState>
        {
            Key = "movies",
            InitialState = new MovieState(),
            Reducers = new MovieReducers(),
            Effects = [new LoadMoviesEffect(moviesService)]
        },
    
        new Slice<TimerState>
        {
            Key = "timer",
            InitialState = new TimerState(),
            Reducers = new TimerReducers()
        },
    
        new Slice<TodoState>
        {
            Key = "todos",
            InitialState = new TodoState(),
            Reducers = new TodoReducers()
        }
    ];

    return new Store(slices, dispatcher);
});

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
