using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class Profiling
{
    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new BreadcrumbItem("Profiling", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private readonly ProfilingGeneratorOptions _options = new();
    private string? _generatedCode;
    private string _namespace = "MyApp.Profiling";

    private async Task GenerateAsync()
    {
        try
        {
            _generatedCode = await Generator.GenerateCodeAsync(_options);
            Snackbar.Add("Profiling code generated successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generating code: {ex.Message}", Severity.Error);
        }
    }

    private async Task CopyToClipboardAsync()
    {
        if (string.IsNullOrEmpty(_generatedCode))
        {
            return;
        }

        await Js.InvokeVoidAsync("navigator.clipboard.writeText", _generatedCode);
        Snackbar.Add("Code copied to clipboard!", Severity.Success);
    }

    private async Task DownloadCodeAsync()
    {
        if (string.IsNullOrEmpty(_generatedCode))
        {
            return;
        }

        const string fileName = "ProfilingCode.cs";
        await Js.InvokeVoidAsync("downloadFile", fileName, _generatedCode);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}
