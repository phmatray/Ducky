# API Data Fetching Example

This example demonstrates how to fetch data from a remote API and manage the asynchronous flow using Ducky in a Blazor WebAssembly app.

## 1. Register Services

In `Program.cs`, register `HttpClient`, your API service, and Ducky:
```C#
// Add services to the container.
builder.Services.AddScoped(_ => new HttpClient 
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddTransient<IMoviesService, MoviesService>();

// Add Ducky
builder.Services.AddDucky(builder.Configuration);

// Run the app
await builder.Build().RunAsync();
```

## 2. Define State & Actions

Before defining actions, specify your slice state shape without defaults. Initial values will be provided in the reducer:

```C#
public record MoviesState(
    bool IsLoading,
    ValueCollection<Movie> Movies,
    int TotalItems,
    string? ErrorMessage
);
```

- `IsLoading` indicates when a fetch is in progress.
- `Movies` holds the current list of movies.
- `TotalItems` tracks the total available count.
- `ErrorMessage` stores any error message.

```C#
[DuckyAction]
public record FetchMovies(
    int PageNumber = 1,
    int PageSize = 5);

[DuckyAction]
public record FetchMoviesSuccess(
    ValueCollection<Movie> Movies
    int TotalItems);

[DuckyAction]
public record FetchMoviesFailure(
    string ErrorMessage);
```

## 3. Reducers

```C#
public class MoviesReducers : SliceReducers<MoviesState>
{
    public MoviesReducers()
    {
        On<FetchMovies>(Reduce);
        On<FetchMoviesSuccess>(Reduce);
        On<FetchMoviesFailure>(Reduce);
    }

    // Initialize all state properties here
    public override MoviesState GetInitialState()
        => new MoviesState(
            IsLoading: false,
            Movies: [],
            TotalItems: 0,
            ErrorMessage: null
        );
    
    private static MoviesState Reduce(MoviesState state, FetchMovies action)
        => state with
        {
            IsLoading = true,
            ErrorMessage = null
        };
        
    private static MoviesState Reduce(MoviesState state, FetchMoviesSuccess action)
        => state with
        {
            IsLoading = false,
            Movies = action.Movies,
            TotalItems = action.TotalItems,
            ErrorMessage = null
        };
        
    private static MoviesState Reduce(MoviesState state, FetchMoviesFailure action)
        => state with
        {
            IsLoading = false,
            ErrorMessage = action.ErrorMessage
        };
}
```

## 4. Effects

```C#
public class FetchMoviesEffect(IMoviesService moviesService)
    : AsyncEffect<FetchMovies>
{
    public override async Task HandleAsync(FetchMovies action, IRootState rootState)
    {
        try
        {
            var response = await moviesService.GetMoviesAsync(action.PageNumber, action.PageSize);
            Dispatcher.FetchMoviesSuccess(response.Movies, response.TotalItems);
        }
        catch (Exception ex)
        {
            Dispatcher.FetchMoviesFailure(ex.Message);
        }
    }
}
```

## 5. Using in a Blazor Component

In the Razor component, inject the store, subscribe to state updates for automatic re-rendering, and dispatch the fetch action on init:

```razor
@page "/movies"
@using Ducky.Blazor
@using Demo.BlazorWasm.AppStore
@inherits DuckyComponent<MoviesState>

<h3>Movie List</h3>
<button @onclick="() => Dispatcher.FetchMovies()">Refresh</button>

@if (State.IsLoading)
{
  <p>Loading movies...</p>
}
else if (!string.IsNullOrEmpty(State.ErrorMessage))
{
  <p class="text-danger">Error: @State.ErrorMessage</p>
}
else
{
  <p>Total movies: @State.TotalItems</p>
  <ul>
    @foreach (var movie in State.Movies)
    {
      <li>@movie.Title (@movie.ReleaseYear)</li>
    }
  </ul>
}

@code {

  // This method is called after the component is subscribed to the state
  protected override void OnAfterSubscribed()
  {
    // Dispatch initial load
    Dispatcher.FetchMovies();
  }
  
}
```

This ensures:
- The component reacts to state changes and re-renders.
- Loading, error, and data states display correctly.

## 6. Run the Demo

```bash
dotnet run --project src/demo/Demo.BlazorWasm
