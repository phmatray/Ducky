using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageMultiSlice
{
    public record DashboardViewModel(
        int Count,
        int MovieCount,
        bool IsLoadingMovies,
        int UnreadNotifications,
        bool HasNotifications);

    protected override DashboardViewModel Select(IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();
        MoviesState moviesState = rootState.GetSliceState<MoviesState>();
        NotificationsState notificationsState = rootState.GetSliceState<NotificationsState>();

        return new DashboardViewModel(
            Count: counterState.Value,
            MovieCount: moviesState.Movies.Count,
            IsLoadingMovies: moviesState.IsLoading,
            UnreadNotifications: notificationsState.Notifications.Count(n => !n.IsRead),
            HasNotifications: notificationsState.Notifications.Any(n => !n.IsRead)
        );
    }

    protected override bool ShouldRender(DashboardViewModel? previous, DashboardViewModel current)
    {
        // Could implement custom logic here to ignore certain changes
        // For example, only re-render if Count or MovieCount changed
        return previous?.Count != current.Count
            || previous?.MovieCount != current.MovieCount
            || previous?.IsLoadingMovies != current.IsLoadingMovies
            || previous?.UnreadNotifications != current.UnreadNotifications;
    }

    private void IncrementCounter()
    {
        Dispatch(new Increment());
    }

    private void LoadMovies()
    {
        Dispatch(new LoadMovies());
    }
}
