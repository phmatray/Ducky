using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ducky.E2E;

// SPEC §17.2: the merge of the CDP precise-coverage collections of ducky.js. Linked into build/_build.csproj, where the
// E2E target gates on it once, after the whole run; the planted check in this project runs it on real V8 output.
internal static class JsBlockCoverage
{
    // Every copy of the module is keyed to ducky.js, whatever host, port, path or query served it, fingerprinted
    // (ducky.<hash>.js, MapStaticAssets with an import map) or not.
    public static bool IsDuckyJs(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && Regex.IsMatch(uri.AbsolutePath, @"/ducky(\.[A-Za-z0-9]+)?\.js\z");

    // Each element is one V8 ScriptCoverage (one loaded copy of the module in one page). V8 drops a nested range whose
    // count equals its parent's, so a block's count in a copy is that of the innermost range of the copy enclosing it
    // (0 when no range does). Returns the blocks whose count summed over every copy is zero, in source order.
    public static List<(int Start, int End)> ZeroBlocks(IEnumerable<JsonElement> scripts)
    {
        var copies = scripts
            .Select(s => s.GetProperty("functions").EnumerateArray()
                .SelectMany(f => f.GetProperty("ranges").EnumerateArray())
                .Select(r => (Start: r.GetProperty("startOffset").GetInt32(), End: r.GetProperty("endOffset").GetInt32(), Count: r.GetProperty("count").GetInt64()))
                .ToList())
            .ToList();
        // ponytail: O(blocks x ranges x copies); fine for one module, index the ranges by offset if it ever shows up in E2E time.
        return copies
            .SelectMany(c => c.Select(r => (r.Start, r.End)))
            .Distinct()
            .Where(b => copies.Sum(c => c
                .Where(r => r.Start <= b.Start && b.End <= r.End)
                .OrderBy(r => r.End - r.Start)
                .Select(r => r.Count)
                .FirstOrDefault()) == 0)
            .OrderBy(b => b.Start)
            .ThenBy(b => b.End)
            .ToList();
    }
}
