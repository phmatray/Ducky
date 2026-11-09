using System.Collections.Frozen;
using System.Net;
using System.Text;

namespace Ducky.Playground.Xray;

// A small C# and Razor highlighter for the code drawer, emitting the site's token classes (tk-kw, tk-type, tk-str,
// tk-com, tk-attr, tk-tag). It returns one HTML string per source line, so the drawer can number and mark lines.
public static class Highlighter
{
    private static readonly FrozenSet<string> _keywords = FrozenSet.ToFrozenSet(
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "case", "catch", "class", "const", "continue",
        "decimal", "default", "do", "double", "else", "enum", "event", "false", "finally", "for", "foreach", "get",
        "if", "in", "init", "int", "interface", "internal", "is", "long", "namespace", "new", "not", "null", "object",
        "and", "or", "out", "override", "params", "partial", "private", "protected", "public", "readonly", "record",
        "required", "return", "sealed", "set", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "using", "var", "virtual", "void", "when", "where", "while", "with", "yield", "nameof", "char",
        "lock", "file", "value",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> _directives = FrozenSet.ToFrozenSet(
    [
        "code", "inject", "inherits", "page", "using", "implements", "attribute", "if", "else", "foreach", "for",
        "switch", "key", "bind", "onclick", "oninput", "onchange", "onkeydown", "ref", "layout",
    ], StringComparer.Ordinal);

    public static IReadOnlyList<string> Lines(string source, bool razor)
    {
        var tokens = Tokenize(source.Replace("\r\n", "\n", StringComparison.Ordinal), razor);
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var (text, css) in tokens)
        {
            var parts = text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                }
                Append(line, parts[i], css);
            }
        }
        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }
        return lines;
    }

    private static void Append(StringBuilder line, string text, string? css)
    {
        if (text.Length == 0)
        {
            return;
        }
        var encoded = WebUtility.HtmlEncode(text);
        if (css is null)
        {
            line.Append(encoded);
        }
        else
        {
            line.Append("<span class=\"").Append(css).Append("\">").Append(encoded).Append("</span>");
        }
    }

    private static List<(string Text, string? Css)> Tokenize(string s, bool razor)
    {
        var tokens = new List<(string, string?)>();
        var plain = new StringBuilder();
        var codeDepth = -1; // brace depth inside a Razor @code block, -1 outside one
        var depth = 0;
        var i = 0;

        void Flush()
        {
            if (plain.Length > 0)
            {
                tokens.Add((plain.ToString(), null));
                plain.Clear();
            }
        }

        void Emit(int start, int end, string css)
        {
            Flush();
            tokens.Add((s[start..end], css));
        }

        while (i < s.Length)
        {
            var c = s[i];
            var next = i + 1 < s.Length ? s[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                var end = s.IndexOf('\n', i);
                end = end < 0 ? s.Length : end;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (c == '/' && next == '*')
            {
                var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 2;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (razor && c == '@' && next == '*')
            {
                var end = s.IndexOf("*@", i + 2, StringComparison.Ordinal);
                end = end < 0 ? s.Length : end + 2;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (c == '"' || ((c == '$' || c == '@') && next == '"') || (c == '$' && next == '@' && i + 2 < s.Length && s[i + 2] == '"'))
            {
                var end = StringEnd(s, i);
                Emit(i, end, "tk-str");
                i = end;
            }
            else if (c == '\'' && CharEnd(s, i) is var charEnd and > 0)
            {
                Emit(i, charEnd, "tk-str");
                i = charEnd;
            }
            else if (razor && c == '@' && (char.IsLetter(next) || next == '_'))
            {
                var end = WordEnd(s, i + 1);
                var word = s[(i + 1)..end];
                if (_directives.Contains(word) || _keywords.Contains(word))
                {
                    Emit(i, end, "tk-kw");
                    if (word == "code")
                    {
                        codeDepth = depth;
                    }
                }
                else
                {
                    plain.Append('@');
                    plain.Append(word);
                }
                i = end;
            }
            else if (razor && codeDepth < 0 && c == '<' && (char.IsLetter(next) || (next == '/' && i + 2 < s.Length && char.IsLetter(s[i + 2]))))
            {
                var start = i;
                i += next == '/' ? 2 : 1;
                var end = WordEnd(s, i);
                Emit(start, end, "tk-tag");
                i = end;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var end = WordEnd(s, i);
                var word = s[i..end];
                var previous = PreviousSignificant(s, i);
                if (_keywords.Contains(word))
                {
                    Emit(i, end, "tk-kw");
                }
                else if (previous == '[' || (previous == ',' && InsideAttribute(s, i)))
                {
                    Emit(i, end, "tk-attr");
                }
                else if (char.IsUpper(word[0]) && IsTypePosition(s, i, end))
                {
                    Emit(i, end, "tk-type");
                }
                else
                {
                    plain.Append(word);
                }
                i = end;
            }
            else
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == codeDepth)
                    {
                        codeDepth = -1;
                    }
                }
                plain.Append(c);
                i++;
            }
        }

        Flush();
        return tokens;
    }

    // A capitalized identifier reads as a type unless it is a member access (x.Name), a call (Name(...), but new Name(...)
    // stays a type) or a name being declared or assigned (Name = ..., Name => ..., Name;).
    private static bool IsTypePosition(string s, int start, int end)
    {
        if (start > 0 && s[start - 1] == '.')
        {
            return false;
        }
        var next = Peek(s, end);
        return next switch
        {
            '(' => PreviousWord(s, start) == "new",
            '=' or ';' => false,
            _ => true,
        };
    }

    private static int WordEnd(string s, int i)
    {
        while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_'))
        {
            i++;
        }
        return i;
    }

    private static char Peek(string s, int i)
    {
        while (i < s.Length && s[i] == ' ')
        {
            i++;
        }
        return i < s.Length ? s[i] : '\0';
    }

    private static char PreviousSignificant(string s, int i)
    {
        i--;
        while (i >= 0 && char.IsWhiteSpace(s[i]))
        {
            i--;
        }
        return i >= 0 ? s[i] : '\0';
    }

    private static string PreviousWord(string s, int i)
    {
        var end = i - 1;
        while (end >= 0 && s[end] == ' ')
        {
            end--;
        }
        var start = end;
        while (start >= 0 && char.IsLetter(s[start]))
        {
            start--;
        }
        return end < 0 ? "" : s[(start + 1)..(end + 1)];
    }

    // True when the identifier at i sits inside an attribute list opened on the same line: [A, B(...)].
    private static bool InsideAttribute(string s, int i)
    {
        var lineStart = s.LastIndexOf('\n', Math.Max(0, i - 1)) + 1;
        return s.AsSpan(lineStart, i - lineStart).TrimStart().StartsWith("[");
    }

    // The end of a string literal starting at i: "...", @"..." (doubled quotes), $"..." (interpolation holes may hold
    // strings) and """raw""".
    private static int StringEnd(string s, int i)
    {
        var verbatim = false;
        while (s[i] != '"')
        {
            verbatim |= s[i] == '@';
            i++;
        }
        if (i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"')
        {
            var close = s.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
            return close < 0 ? s.Length : close + 3;
        }

        i++;
        var holes = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '\n' && !verbatim)
            {
                return i;
            }
            if (c == '\\' && !verbatim)
            {
                i += 2;
                continue;
            }
            if (c == '{')
            {
                holes++;
            }
            else if (c == '}' && holes > 0)
            {
                holes--;
            }
            else if (c == '"' && holes == 0)
            {
                if (verbatim && i + 1 < s.Length && s[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return s.Length;
    }

    // 'x' or '\x' only, so an apostrophe in Razor markup text ("don't") stays text. Returns the end, or -1.
    private static int CharEnd(string s, int i)
    {
        if (i + 2 < s.Length && s[i + 1] != '\\' && s[i + 2] == '\'')
        {
            return i + 3;
        }
        if (i + 3 < s.Length && s[i + 1] == '\\' && s[i + 3] == '\'')
        {
            return i + 4;
        }
        return -1;
    }
}
