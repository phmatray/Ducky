// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

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

    protected override DashboardViewModel Select(IStateProvider stateProvider)
    {
        CounterState counterState = stateProvider.GetSlice<CounterState>();
        MoviesState moviesState = stateProvider.GetSlice<MoviesState>();
        NotificationsState notificationsState = stateProvider.GetSlice<NotificationsState>();

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
