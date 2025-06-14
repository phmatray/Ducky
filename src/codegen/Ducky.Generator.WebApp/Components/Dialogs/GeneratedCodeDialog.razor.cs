using System.Text;
using Ducky.Generator.WebApp.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Dialogs;

public partial class GeneratedCodeDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public List<GeneratedFile> GeneratedFiles { get; set; } = [];

    [Parameter]
    public string AppStoreName { get; set; } = string.Empty;

    private void Close() => MudDialog.Close();

    private Color GetFileTypeColor(string fileType) => fileType switch
    {
        "State" => Color.Info,
        "Actions" => Color.Success,
        "Reducers" => Color.Primary,
        "Effects" => Color.Warning,
        "Duck" => Color.Secondary,
        "Configuration" => Color.Tertiary,
        _ => Color.Default
    };

    private string GetFileTypeIcon(string fileType) => fileType switch
    {
        "State" => Icons.Material.Filled.DataObject,
        "Actions" => Icons.Material.Filled.PlayArrow,
        "Reducers" => Icons.Material.Filled.Transform,
        "Effects" => Icons.Material.Filled.Bolt,
        "Duck" => Icons.Material.Filled.Category,
        "Configuration" => Icons.Material.Filled.Settings,
        _ => Icons.Material.Filled.Description
    };

    private string GetFileIcon(string fileName) => fileName.EndsWith(".cs") switch
    {
        true => Icons.Material.Filled.Code,
        _ => Icons.Material.Filled.Description
    };

    private int GetFileTypeOrder(string fileType) => fileType switch
    {
        "State" => 1,
        "Actions" => 2,
        "Reducers" => 3,
        "Effects" => 4,
        "Duck" => 5,
        "Configuration" => 6,
        _ => 99
    };

    private string GetFileTypeDisplay(string fileType) => fileType switch
    {
        "Duck" => "Ducks",
        _ => fileType
    };

    private async Task CopyToClipboardAsync(string content, string fileName)
    {
        await Js.InvokeVoidAsync("navigator.clipboard.writeText", content);
        Snackbar.Add($"Copied {fileName} to clipboard!", Severity.Success);
    }

    private async Task CopyAllToClipboardAsync()
    {
        string allContent = CreateCombinedContent();
        await Js.InvokeVoidAsync("navigator.clipboard.writeText", allContent);
        Snackbar.Add($"Copied all {GeneratedFiles.Count} files to clipboard!", Severity.Success);
    }

    private async Task DownloadFileAsync(GeneratedFile file)
    {
        await Js.InvokeVoidAsync("downloadFile", file.FileName, file.Content);
        Snackbar.Add($"Downloaded {file.FileName}", Severity.Success);
    }

    private async Task DownloadAllAsync()
    {
        string combinedContent = CreateCombinedContent();
        string fileName = $"{AppStoreName}_Generated_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

        await Js.InvokeVoidAsync("downloadFile", fileName, combinedContent);
        Snackbar.Add($"Downloaded all files as {fileName}", Severity.Success);
    }

    private string CreateCombinedContent()
    {
        StringBuilder sb = new();
        sb.AppendLine($"// Generated Ducky Code for: {AppStoreName}");
        sb.AppendLine($"// Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Total files: {GeneratedFiles.Count}");
        sb.AppendLine(new string('=', 80));
        sb.AppendLine();

        IOrderedEnumerable<IGrouping<string, GeneratedFile>> orderedEnumerable = GeneratedFiles
            .GroupBy(f => f.FileType)
            .OrderBy(g => GetFileTypeOrder(g.Key));

        foreach (IGrouping<string, GeneratedFile> fileGroup in orderedEnumerable)
        {
            sb.AppendLine($"// {fileGroup.Key} Files");
            sb.AppendLine(new string('-', 40));

            foreach (GeneratedFile file in fileGroup.OrderBy(f => f.FileName))
            {
                sb.AppendLine();
                sb.AppendLine($"// File: {file.FileName}");
                sb.AppendLine($"// Type: {file.FileType}");
                sb.AppendLine(new string('-', 40));
                sb.AppendLine(file.Content);
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
