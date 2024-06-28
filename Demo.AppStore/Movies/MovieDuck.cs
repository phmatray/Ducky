namespace Demo.AppStore;

// State
public record MovieState
{
    public ImmutableArray<Movie> Movies { get; init; } = [];
    public bool IsLoading { get; init; }
    public Exception? Error { get; init; }
    
    // 5 more recent movies
    public ImmutableArray<Movie> MoviesByYear =>
    [
        ..Movies
            .Sort((a, b) => a.Year.CompareTo(b.Year))
            .TakeLast(5)
            .Reverse()
    ];
}

// Actions
public record LoadMovies : IAction;
public record LoadMoviesSuccess(ImmutableArray<Movie> Movies) : IAction;
public record LoadMoviesFailure(Exception Error) : IAction;

// Reducer
public class MovieReducer : Reducer<MovieState>
{
    public MovieReducer()
    {
        Register<LoadMovies>((state, _)
            => state with { IsLoading = true, Error = null });
        
        Register<LoadMoviesSuccess>((state, action)
            => state with { Movies = action.Movies, IsLoading = false });
        
        Register<LoadMoviesFailure>((state, action)
            => new MovieState { Movies = [], Error = action.Error, IsLoading = false });
    }
}

// Effects
public class LoadMoviesEffect(MoviesService moviesService)
    : Effect<MovieState>
{
    /// <inheritdoc />
    public override Observable<IAction> Handle(
        Observable<IAction> actions, Observable<MovieState> state)
    {
        return actions
            .FilterActions<LoadMovies>()
            .LogMessage("Loading movies...")
            .InvokeService(
                action => moviesService.GetMoviesAsync(),
                movies => new LoadMoviesSuccess(movies),
                ex => new LoadMoviesFailure(ex))
            .LogMessage("Movies loaded.");

        // THE SAME CODE CAN BE WRITTEN WITHOUT THE EXTENSION METHODS
        // ============================================================
        // return actions
        //     .OfType<IAction, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .SelectMany(action => moviesService
        //         .GetMoviesAsync()
        //         .ToObservable()
        //         .Select(movies => (IAction)new LoadMoviesSuccess(movies))
        //         .Do(_ => Console.WriteLine("Movies loaded.")))
        //     .Catch<IAction, Exception>(ex => Observable.Return<IAction>(new LoadMoviesFailure(ex)));

        // THE FOLLOWING CODE WORKS AS AN ALTERNATIVE TO THE ABOVE CODE
        // ============================================================
        // return actions
        //     .OfType<IAction, LoadMovies>()
        //     .Do(_ => Console.WriteLine("Loading movies..."))
        //     .SelectAwait(async (action, ct) =>
        //     {
        //         try
        //         {
        //             var movies = await moviesService.GetMoviesAsync(ct);
        //             return new LoadMoviesSuccess(movies) as IAction;
        //         }
        //         catch (Exception ex)
        //         {
        //             return new LoadMoviesFailure(ex);
        //         }
        //     });
    }
}
