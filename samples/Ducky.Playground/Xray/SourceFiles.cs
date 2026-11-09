namespace Ducky.Playground.Xray;

// One source file: its plain lines (to find the focus) and its highlighted lines (to show).
public sealed record SourceText(IReadOnlyList<string> Plain, IReadOnlyList<string> Html)
{
    public int Find(string focus)
    {
        for (var n = 0; n < Plain.Count; n++)
        {
            if (Plain[n].Contains(focus, StringComparison.Ordinal))
            {
                return n;
            }
        }
        return -1;
    }
}

// The drawer's source: the app's own files, served beside it under _source/ by the build (Ducky.Playground.csproj), read
// once and highlighted.
public sealed class SourceFiles(HttpClient http)
{
    private readonly Dictionary<string, Task<SourceText?>> _cache = new(StringComparer.Ordinal);

    public Task<SourceText?> ReadAsync(string path)
    {
        if (!_cache.TryGetValue(path, out var source))
        {
            source = LoadAsync(path);
            _cache[path] = source;
        }
        return source;
    }

    private async Task<SourceText?> LoadAsync(string path)
    {
        try
        {
            var text = (await http.GetStringAsync($"_source/{path}.txt")).Replace("\r\n", "\n", StringComparison.Ordinal);
            return new SourceText(text.Split('\n'), Highlighter.Lines(text, razor: path.EndsWith(".razor", StringComparison.Ordinal)));
        }
        catch (HttpRequestException)
        {
            _cache.Remove(path);
            return null;
        }
    }
}
