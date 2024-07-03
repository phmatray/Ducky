namespace Demo.AppStore;

// State
public record MovieState
{
    public ImmutableArray<Movie> Movies { get; init; } = [];
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }
    
    // Selectors
    // ==========
    // We can define selectors as methods in the state record
    // to encapsulate the logic of selecting data from the state.
    // Each method should begin with the word "Select".
    public int SelectMovieCount()
        => Movies.Length;

    public ImmutableArray<Movie> SelectMoviesByYear(int takeCount = 5)
        => Movies
            .Sort((a, b) => a.Year.CompareTo(b.Year))
            .TakeLast(takeCount)
            .Reverse()
            .ToImmutableArray();
}

// Actions
public record LoadMovies;
public record LoadMoviesSuccess(ImmutableArray<Movie> Movies);
public record LoadMoviesFailure(string ErrorMessage);

// Reducers
public class MovieReducers : ReducerCollection<MovieState>
{
    public MovieReducers()
    {
        Map<LoadMovies>((state, _)
            => state with { IsLoading = true, ErrorMessage = null });
        
        Map<LoadMoviesSuccess>((state, action)
            => state with { Movies = action.Movies, IsLoading = false });
        
        Map<LoadMoviesFailure>((state, action)
            => new MovieState { Movies = [], ErrorMessage = action.ErrorMessage, IsLoading = false });
    }
}

// Effects
public class LoadMoviesEffect(MoviesService moviesService) : Effect
{
    public override Observable<object> Handle(
        Observable<object> actions, Store store)
    {
        return actions
            .FilterActions<LoadMovies>()
            .LogMessage("Loading movies...")
            .InvokeService(
                action => moviesService.GetMoviesAsync(),
                movies => new LoadMoviesSuccess(movies),
                ex => new LoadMoviesFailure(ex.Message))
            .LogMessage("Movies loaded.");

        // THE SAME CODE CAN BE WRITTEN WITHOUT THE EXTENSION METHODS
        // ============================================================
        // return actions
        //     .OfType<object, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .SelectMany(action => moviesService
        //         .GetMoviesAsync()
        //         .ToObservable()
        //         .Select(movies => (object)new LoadMoviesSuccess(movies))
        //         .Do(_ => Console.WriteLine("Movies loaded."))
        //         .Catch<object, Exception>(ex => Observable.Return<object>(new LoadMoviesFailure(ex.Message))));

        // THE FOLLOWING CODE WORKS AS AN ALTERNATIVE TO THE ABOVE CODE
        // ============================================================
        // return actions
        //     .OfType<object, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .SelectAwait(async (action, ct) =>
        //     {
        //         try
        //         {
        //             var movies = await moviesService.GetMoviesAsync(ct);
        //             return new LoadMoviesSuccess(movies) as object;
        //         }
        //         catch (Exception ex)
        //         {
        //             return new LoadMoviesFailure(ex.Message);
        //         }
        //     });
    }
}
