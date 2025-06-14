using Microsoft.AspNetCore.Components;

namespace Demo.BlazorWasm.Components.Shared;

public partial class LoadingSkeleton
{
    [Parameter]
    public int Rows { get; set; } = 5;

    [Parameter]
    public int Columns { get; set; } = 4;

    [Parameter]
    public string[] ColumnWidths { get; set; } = Array.Empty<string>();

    private string GetSkeletonClass(int columnIndex)
    {
        return columnIndex == 0 ? "skeleton-text" : "skeleton-cell";
    }

    private string GetSkeletonStyle(int columnIndex)
    {
        if (ColumnWidths.Length > columnIndex)
        {
            return $"width: {ColumnWidths[columnIndex]}";
        }

        return columnIndex == 0 ? "width: 40%" : "width: 15%";
    }
}
