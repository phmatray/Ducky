using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ducky.Playground.Inspector;

public sealed record DiffLine(string Path, string? Before, string? After);

// The changed leaves between two JSON documents of a slice's state, for the inspector's detail panel.
public static class StateDiff
{
    public const int MaxLines = 60;

    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static IReadOnlyList<DiffLine> Compare(JsonNode? before, JsonNode? after, string root)
    {
        var lines = new List<DiffLine>();
        Walk(before, after, root, lines);
        return lines;
    }

    private static void Walk(JsonNode? before, JsonNode? after, string path, List<DiffLine> lines)
    {
        if (lines.Count >= MaxLines || JsonNode.DeepEquals(before, after))
        {
            return;
        }

        switch (before, after)
        {
            case (JsonObject left, JsonObject right):
                foreach (var key in left.Select(static p => p.Key).Union(right.Select(static p => p.Key), StringComparer.Ordinal))
                {
                    Walk(left[key], right[key], $"{path}.{key}", lines);
                }
                break;
            case (JsonArray left, JsonArray right):
                for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
                {
                    Walk(i < left.Count ? left[i] : null, i < right.Count ? right[i] : null, $"{path}[{i}]", lines);
                }
                break;
            default:
                lines.Add(new DiffLine(path, before?.ToJsonString(), after?.ToJsonString()));
                break;
        }
    }
}
