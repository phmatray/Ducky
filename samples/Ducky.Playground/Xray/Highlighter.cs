using System.Collections.Frozen;
using System.Net;
using System.Text;

namespace Ducky.Playground.Xray;

// A small C# and Razor highlighter for the code drawer, emitting the site's token
// classes (tk-kw, tk-type, tk-str, tk-com, tk-attr, tk-tag). It returns one HTML string
// per source line, so the drawer can number and mark lines.
public static class Highlighter
{
    private static readonly FrozenSet<string> _keywords = FrozenSet.ToFrozenSet(
    [
        "abstract", "and", "as", "async", "await", "base", "bool", "break", "case",
        "catch", "char", "class", "const", "continue", "decimal", "default", "do",
        "double", "else", "enum", "event", "false", "file", "finally", "for", "foreach",
        "get", "if", "in", "init", "int", "interface", "internal", "is", "lock", "long",
        "nameof", "namespace", "new", "not", "null", "object", "or", "out", "override",
        "params", "partial", "private", "protected", "public", "readonly", "record",
        "required", "return", "sealed", "set", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "using", "value", "var", "virtual",
        "void", "when", "where", "while", "with", "yield",
    ], StringComparer.Ordinal);

    private static readonly FrozenSet<string> _directives = FrozenSet.ToFrozenSet(
    [
        "code", "inject", "inherits", "page", "using", "implements", "attribute", "if",
        "else", "foreach", "for", "switch", "key", "bind", "onclick", "oninput",
        "onchange", "onkeydown", "ref", "layout",
    ], StringComparer.Ordinal);

    public static IReadOnlyList<string> Lines(string source, bool razor)
    {
        var unixSource = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var tokens = Tokenize(unixSource, razor);
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
            line.Append("<span class=\"").Append(css).Append("\">")
                .Append(encoded)
                .Append("</span>");
        }
    }

    private static List<(string Text, string? Css)> Tokenize(string source, bool razor)
    {
        var tokens = new List<(string, string?)>();
        var plain = new StringBuilder();
        // The brace depth where a Razor @code block opened, or -1 outside one: markup
        // tags are only highlighted outside @code.
        var codeDepth = -1;
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
            tokens.Add((source[start..end], css));
        }

        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                var end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (c == '/' && next == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (razor && c == '@' && next == '*')
            {
                var end = source.IndexOf("*@", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                Emit(i, end, "tk-com");
                i = end;
            }
            else if (StartsString(source, i))
            {
                var end = StringEnd(source, i);
                Emit(i, end, "tk-str");
                i = end;
            }
            else if (c == '\'' && CharEnd(source, i) is var charEnd and > 0)
            {
                Emit(i, charEnd, "tk-str");
                i = charEnd;
            }
            else if (razor && c == '@' && (char.IsLetter(next) || next == '_'))
            {
                var end = WordEnd(source, i + 1);
                var word = source[(i + 1)..end];
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
            else if (razor && codeDepth < 0 && StartsTag(source, i))
            {
                var start = i;
                i += next == '/' ? 2 : 1;
                var end = WordEnd(source, i);
                Emit(start, end, "tk-tag");
                i = end;
            }
            else if (char.IsLetter(c) || c == '_')
            {
                var end = WordEnd(source, i);
                var word = source[i..end];
                var previous = PreviousSignificant(source, i);
                if (_keywords.Contains(word))
                {
                    Emit(i, end, "tk-kw");
                }
                else if (previous == '['
                    || (previous == ',' && InsideAttribute(source, i)))
                {
                    Emit(i, end, "tk-attr");
                }
                else if (char.IsUpper(word[0]) && IsTypePosition(source, i, end))
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

    // "...", $"...", @"..." and $@"...".
    private static bool StartsString(string source, int i)
    {
        var c = source[i];
        var next = i + 1 < source.Length ? source[i + 1] : '\0';
        return c == '"'
            || ((c == '$' || c == '@') && next == '"')
            || (c == '$' && next == '@' && i + 2 < source.Length && source[i + 2] == '"');
    }

    // <tag or </tag.
    private static bool StartsTag(string source, int i)
    {
        if (source[i] != '<' || i + 1 >= source.Length)
        {
            return false;
        }
        var next = source[i + 1];
        return char.IsLetter(next)
            || (next == '/' && i + 2 < source.Length && char.IsLetter(source[i + 2]));
    }

    // A capitalized identifier reads as a type unless it is a member access (x.Name), a
    // call (Name(...), but new Name(...) stays a type) or a name being declared or
    // assigned (Name = ..., Name => ..., Name;).
    private static bool IsTypePosition(string source, int start, int end)
    {
        if (start > 0 && source[start - 1] == '.')
        {
            return false;
        }
        var next = Peek(source, end);
        return next switch
        {
            '(' => PreviousWord(source, start) == "new",
            '=' or ';' => false,
            _ => true,
        };
    }

    private static int WordEnd(string source, int i)
    {
        while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_'))
        {
            i++;
        }
        return i;
    }

    private static char Peek(string source, int i)
    {
        while (i < source.Length && source[i] == ' ')
        {
            i++;
        }
        return i < source.Length ? source[i] : '\0';
    }

    private static char PreviousSignificant(string source, int i)
    {
        i--;
        while (i >= 0 && char.IsWhiteSpace(source[i]))
        {
            i--;
        }
        return i >= 0 ? source[i] : '\0';
    }

    private static string PreviousWord(string source, int i)
    {
        var end = i - 1;
        while (end >= 0 && source[end] == ' ')
        {
            end--;
        }
        var start = end;
        while (start >= 0 && char.IsLetter(source[start]))
        {
            start--;
        }
        return end < 0 ? "" : source[(start + 1)..(end + 1)];
    }

    // True when the identifier at i sits inside an attribute list opened on the same
    // line: [A, B(...)].
    private static bool InsideAttribute(string source, int i)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, i - 1)) + 1;
        return source.AsSpan(lineStart, i - lineStart).TrimStart().StartsWith("[");
    }

    // The end of a string literal starting at i: "...", @"..." (doubled quotes), $"..."
    // (interpolation holes may hold strings) and """raw""".
    private static int StringEnd(string source, int i)
    {
        var verbatim = false;
        while (source[i] != '"')
        {
            verbatim |= source[i] == '@';
            i++;
        }
        if (i + 2 < source.Length && source[i + 1] == '"' && source[i + 2] == '"')
        {
            var close = source.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
            return close < 0 ? source.Length : close + 3;
        }

        i++;
        var holes = 0;
        while (i < source.Length)
        {
            var c = source[i];
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
                if (verbatim && i + 1 < source.Length && source[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }
                return i + 1;
            }
            i++;
        }
        return source.Length;
    }

    // 'x' or '\x' only, so an apostrophe in Razor markup text ("don't") stays text.
    // Returns the end, or -1.
    private static int CharEnd(string source, int i)
    {
        if (i + 2 < source.Length && source[i + 1] != '\\' && source[i + 2] == '\'')
        {
            return i + 3;
        }
        if (i + 3 < source.Length && source[i + 1] == '\\' && source[i + 3] == '\'')
        {
            return i + 4;
        }
        return -1;
    }
}
