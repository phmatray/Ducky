using Demo.BlazorWasm.AppStore;
using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageMovieDetails
{
    [Parameter]
    public required int Id { get; set; }

    private Movie? SelectedMovie
        => State.SelectMovieById(Id);
}
