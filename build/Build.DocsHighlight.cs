using System.Net;
using System.Text;
using System.Text.RegularExpressions;

// Build-time syntax highlighting for the docs site: the home page's token classes (tk-kw, tk-type, tk-str, tk-com,
// tk-attr, tk-tag), so a snippet reads the same in both, with no script needed to colour it.
internal sealed partial class Build
{
    private static readonly HashSet<string> CSharpKeywords =
    [
        "abstract", "and", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "dynamic", "else", "enum", "event",
        "explicit", "extern", "false", "file", "finally", "fixed", "float", "for", "foreach", "get", "global", "goto", "if",
        "implicit", "in", "init", "int", "interface", "internal", "is", "let", "lock", "long", "managed", "nameof",
        "namespace", "new", "not", "notnull", "null", "object", "operator", "or", "out", "override", "params", "partial",
        "private", "protected", "public", "readonly", "record", "ref", "remove", "required", "return", "sbyte", "scoped",
        "sealed", "set", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unmanaged", "unsafe", "ushort", "using", "value", "var", "virtual",
        "void", "volatile", "when", "where", "while", "with", "yield", "add", "from", "select", "orderby", "group", "into",
        "join", "on", "equals", "by", "ascending", "descending",
    ];

    // One alternation per token class, in priority order: comments, strings (raw, verbatim, interpolated, regular,
    // char), attributes at a line or parameter start, then (Razor only) transitions and markup tags, then identifiers.
    private const string CommentStringAttribute = """
        (?<com>//[^\n]*|/\*.*?\*/)
        |(?<str>\$*"{3,}.*?"{3,}|\$?@"(?:[^"]|"")*"|@\$"(?:[^"]|"")*"|\$?"(?:[^"\\\n]|\\.)*"|'(?:[^'\\\n]|\\.)')
        |(?<attr>(?<=^[ \t]*|[(,][ \t]*)\[[A-Z][A-Za-z0-9.]*(?:\([^\]\n]*\))?\])
        """;

    private const string Word = """
        |(?<word>\b[A-Za-z_][A-Za-z0-9_]*\b)
        """;

    private const string RazorMarkup = """
        |(?<razor>@(?:[a-zA-Z]+|\{|\())
        |(?<tag></?[a-zA-Z][\w.-]*|/?>)
        """;

    private const RegexOptions TokenOptions = RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline | RegexOptions.Multiline;

    private static readonly Regex CSharpToken = new(CommentStringAttribute + Word, TokenOptions);

    private static readonly Regex RazorToken = new(CommentStringAttribute + RazorMarkup + Word, TokenOptions);

    private static readonly Regex ShellToken = new(@"(?<com>(?<=^|[ \t])#[^\n]*)|(?<str>""(?:[^""\\\n]|\\.)*""|'[^'\n]*')", RegexOptions.Multiline);

    internal static string HighlightCode(string code, string language) => language switch
    {
        "csharp" or "cs" or "c#" => Highlight(code, CSharpToken),
        "razor" or "cshtml" => Highlight(code, RazorToken),
        "shell" or "bash" or "sh" or "console" or "powershell" => Highlight(code, ShellToken),
        _ => WebUtility.HtmlEncode(code),
    };

    private static string Highlight(string code, Regex tokens)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match m in tokens.Matches(code))
        {
            html.Append(WebUtility.HtmlEncode(code[last..m.Index]));
            last = m.Index + m.Length;
            var cls = TokenClass(m, code);
            var text = WebUtility.HtmlEncode(m.Value);
            html.Append(cls is null ? text : $"<span class=\"{cls}\">{text}</span>");
        }
        html.Append(WebUtility.HtmlEncode(code[last..]));
        return html.ToString();
    }

    private static string? TokenClass(Match m, string code)
    {
        if (m.Groups["com"].Success)
        {
            return "tk-com";
        }
        if (m.Groups["str"].Success)
        {
            return "tk-str";
        }
        if (m.Groups["attr"].Success)
        {
            return "tk-attr";
        }
        if (m.Groups["razor"].Success)
        {
            return "tk-kw";
        }
        if (m.Groups["tag"].Success)
        {
            return "tk-tag";
        }
        var word = m.Value;
        if (CSharpKeywords.Contains(word))
        {
            return "tk-kw";
        }
        if (!char.IsUpper(word[0]))
        {
            return null;
        }
        // A capitalised name followed by '(' is a method call, unless it follows `new` (a constructor names its type).
        var after = m.Index + m.Length;
        var call = after < code.Length && code[after] == '(';
        var newed = Regex.IsMatch(code[Math.Max(0, m.Index - 12)..m.Index], @"\bnew\s+$");
        return call && !newed ? null : "tk-type";
    }
}
