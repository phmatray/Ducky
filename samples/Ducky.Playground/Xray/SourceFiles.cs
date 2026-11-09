namespace Ducky.Playground.Xray;

// One source file: plain lines to search for the focus, highlighted lines to show.
public sealed record SourceText(
    IReadOnlyList<string> PlainLines,
    IReadOnlyList<string> HtmlLines)
{
    private const string CommentSpan = "<span class=\"tk-com\">";

    // The first line of code that contains the focus text, or -1. Comment lines are
    // skipped, so a file's opening comment can name the feature without taking the mark.
    public int FindFocusLine(string focus)
    {
        for (var index = 0; index < PlainLines.Count && index < HtmlLines.Count; index++)
        {
            if (PlainLines[index].Contains(focus, StringComparison.Ordinal)
                && !IsCommentOnly(HtmlLines[index]))
            {
                return index;
            }
        }
        return -1;
    }

    // The highlighter wraps every comment, line by line, in a single tk-com span.
    private static bool IsCommentOnly(string htmlLine)
    {
        var line = htmlLine.TrimStart();
        return line.StartsWith(CommentSpan, StringComparison.Ordinal)
            && line.IndexOf("<span", CommentSpan.Length, StringComparison.Ordinal) < 0;
    }
}

// The drawer's source: the app's own files, which the build copies beside it under
// _source/ (see Ducky.Playground.csproj). Each file is fetched once and highlighted.
public sealed class SourceFiles(HttpClient http)
{
    private readonly Dictionary<string, Task<SourceText?>> _cache =
        new(StringComparer.Ordinal);

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
            var text = await http.GetStringAsync($"_source/{path}.txt");
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
            var isRazor = path.EndsWith(".razor", StringComparison.Ordinal);
            return new SourceText(text.Split('\n'), Highlighter.Lines(text, isRazor));
        }
        catch (HttpRequestException)
        {
            // Forget the failure, so opening the file again retries.
            _cache.Remove(path);
            return null;
        }
    }
}
