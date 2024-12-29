// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

public interface IMoviesService
{
    ValueTask<GetMoviesResponse> GetMoviesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);
}

public class MoviesService : IMoviesService
{
    public async ValueTask<GetMoviesResponse> GetMoviesAsync(
        int pageNumber = 1,
        int pageSize = 5,
        CancellationToken ct = default)
    {
        await Task.Delay(1000, ct).ConfigureAwait(false);

        // Error on page 3
        if (pageNumber == 3)
        {
            throw new MovieException("Failed to load movies");
        }

        // Calculate the total number of movies and the start index for the requested page.
        int totalMovies = MoviesExamples.Movies.Count;
        int startIndex = (pageNumber - 1) * pageSize;

        // Ensure the start index is within the range of the total number of movies.
        if (startIndex >= totalMovies)
        {
            return new GetMoviesResponse([], totalMovies);
        }

        // Calculate the number of movies to return on the current page.
        int moviesToTake = Math.Min(pageSize, totalMovies - startIndex);

        // Retrieve the paginated list of movies.
        ValueCollection<Movie> paginatedMovies = [
            ..MoviesExamples.Movies
                .Skip(startIndex)
                .Take(moviesToTake)
        ];

        return new GetMoviesResponse(paginatedMovies, totalMovies);
    }
}

public record GetMoviesResponse(
    ValueCollection<Movie> Movies,
    int TotalItems);
