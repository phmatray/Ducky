using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using BlazorAppRxStore.Services;

namespace BlazorAppRxStore.Store;

// State
public record MovieState
{
    public IImmutableList<Movie> Movies { get; init; } = [];
    
    public bool IsLoading { get; init; }
    
    public string? Error { get; init; }
}

// Actions
public record LoadMovies : IAction;

public record LoadMoviesSuccess(IImmutableList<Movie> Movies) : IAction;

public record LoadMoviesFailure(string Error) : IAction;

// Reducer
public class MovieReducer : ActionReducer<MovieState>
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

// Effects
// public class MovieEffects : EffectsBase<MovieState>
// {
//     private readonly MovieService _movieService;
//     
//     public MovieEffects(IObservable<IAction> actionsObservable, MovieService movieService)
//         : base(actionsObservable)
//     {
//         _movieService = movieService;
//
//         var loadMoviesObs = CreateEffect<LoadMovies>(
//             () => actionsObservable.OfType<LoadMovies>()
//                 .SelectMany(_movieService.GetMoviesAsync().ToObservable())
//                 .Select(movies => new LoadMoviesSuccess(movies))
//                 .Catch<IAction, Exception>(ex => Observable.Return(new LoadMoviesFailure(ex.Message)))
//         );
//
//         AddEffect(loadMoviesObs);
//     }
// }
