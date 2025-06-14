using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageMovies
{
    private string _searchTerm = string.Empty;

    private ImmutableDictionary<int, Movie> Movies
        => State.SelectMoviesByYear();

    private bool IsLoading
        => State.IsLoading;

    private string? ErrorMessage
        => State.ErrorMessage;

    private int TotalPages
        => State.Pagination.TotalPages;

    private IEnumerable<Movie> FilteredMovies
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchTerm))
            {
                return Movies.Values;
            }

            string searchLower = _searchTerm.ToLowerInvariant();
            return Movies.Values.Where(m =>
                m.Title.ToLowerInvariant().Contains(searchLower)
                || m.Director.ToLowerInvariant().Contains(searchLower)
                || m.Year.ToString().Contains(searchLower));
        }
    }

    protected override void OnAfterSubscribed()
    {
        if (Movies.Count != 0)
        {
            return;
        }

        LoadMovies();
    }

    private void LoadMovies()
    {
        Dispatcher.LoadMovies();
    }

    private void SetCurrentPage(int page)
    {
        Dispatcher.SetCurrentPage(page);
        Dispatcher.LoadMovies();
    }

    private void GoToMovieDetails(int movieId)
    {
        Navigation.NavigateTo($"/movies/{movieId}");
    }

    private void OnSearchMovies(string searchTerm)
    {
        _searchTerm = searchTerm;
        InvokeAsync(StateHasChanged);
    }
}
