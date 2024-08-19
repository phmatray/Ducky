// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using static Demo.Website2.Features.JsonColoring.Helpers.HtmlSpanHelper;

namespace Demo.Website2.Features.JsonColoring.Services;

/// <inheritdoc />
public class JsonColorizer : IJsonColorizer
{
    /// <summary>
    /// Gets the break line element.
    /// </summary>
    public static string BreakLine => "<br>";

    /// <inheritdoc />
    public string ColorizeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var sb = new IndentedStringBuilder();
        ProcessElement(document.RootElement, sb);
        return sb.ToString();
    }

    private void ProcessElement(
        JsonElement element,
        IndentedStringBuilder sb,
        bool inArray = false)
    {
        if (inArray)
        {
            sb.AppendIndentation();
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ProcessObject(element, sb);
                break;
            case JsonValueKind.Array:
                ProcessArray(element, sb);
                break;
            case JsonValueKind.String:
                sb.Append(SpanJsonString(element.GetString() ?? string.Empty));
                break;
            case JsonValueKind.Number:
                sb.Append(SpanJsonNumber(element.GetRawText()));
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append(SpanJsonBool(element.GetRawText()));
                break;
            case JsonValueKind.Null:
                sb.Append(SpanJsonNull());
                break;
        }
    }

    private void ProcessObject(JsonElement element, IndentedStringBuilder sb)
    {
        sb.Append(SpanOpenBrace());
        sb.Append(BreakLine);
        sb.Indent();

        var properties = element.EnumerateObject().ToArray();
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            sb.AppendIndentation();
            sb.Append($"{SpanJsonPropertyName(property.Name)}: ");
            ProcessElement(property.Value, sb);
            sb.Append(i < properties.Length - 1 ? $",{BreakLine}" : BreakLine);
        }

        sb.Unindent();
        sb.AppendIndentation();
        sb.Append(SpanCloseBrace());
    }

    private void ProcessArray(JsonElement element, IndentedStringBuilder sb)
    {
        sb.Append(SpanOpenBracket());

        var items = element.EnumerateArray().ToArray();
        if (items.Length != 0)
        {
            sb.Append(BreakLine);
            sb.Indent();

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                ProcessElement(item, sb, true);
                sb.Append(i < items.Length - 1 ? $",{BreakLine}" : BreakLine);
            }

            sb.Unindent();
            sb.AppendIndentation();
        }

        sb.Append(SpanCloseBracket());
    }
}
