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
// public class LoadMoviesEffect : Effect<MoviesState>
// {
//     private readonly MoviesService _moviesService = new();
//
//     public override Observable<StoreAction> Handle(
//         Observable<StoreAction> actions)
//     {
//         var observable = _moviesService
//             .GetMoviesAsync()
//             .ToObservable()
//             .Select(movies => new LoadMoviesSuccess([..movies]))
//             .Cast<LoadMoviesSuccess, StoreAction>()
//             .Catch<StoreAction, Exception>(ex => Observable
//                 .Return(new LoadMoviesFailure(ex))
//                 .Cast<LoadMoviesFailure, StoreAction>());
//
//         return actions
//             .OfType<StoreAction, LoadMovies>()
//             .Merge(observable);
//     }
// }