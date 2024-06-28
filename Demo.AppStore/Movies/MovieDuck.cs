namespace Demo.AppStore;

// State
public record MovieState
{
    public ImmutableArray<Movie> Movies { get; init; } = [];
    public bool IsLoading { get; init; }
    public Exception? Error { get; init; }
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
            => state with { Error = action.Error, IsLoading = false });
    }
}

// Service

// Effects
// loadItems$ = createEffect(() => this.actions$.pipe(
//   ofType(ItemActions.loadItems),
//   mergeMap(() => this.itemService.getItems().pipe(
//     map(items => ItemActions.loadItemsSuccess({ items })),
//     catchError(error => of(ItemActions.loadItemsFailure({ error })))
//   ))
// ));
// public class LoadMoviesEffect : Effect<MovieState>
// {
//     private readonly MoviesService _moviesService;
//     
//     public LoadMoviesEffect(MoviesService moviesService)
//     {
//         _moviesService = moviesService;
//     }
//
//     /// <inheritdoc />
//     public override Observable<IAction> Handle(
//         Observable<IAction> actions, Observable<MovieState> state)
//     {
//         var observable = _moviesService
//             .GetMoviesAsync()
//             .ToObservable()
//             .Select(movies => new LoadMoviesSuccess([..movies]))
//             .Cast<LoadMoviesSuccess, IAction>()
//             .Catch<IAction, Exception>(ex => Observable
//                 .Return(new LoadMoviesFailure(ex))
//                 .Cast<LoadMoviesFailure, IAction>());
//
//         return actions
//             .OfType<IAction, LoadMovies>()
//             .Merge(observable);
//     }
// }