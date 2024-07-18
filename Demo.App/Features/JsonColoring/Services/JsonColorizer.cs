using System.Text.Json;
using static Demo.App.Features.JsonColoring.HtmlSpanHelper;

namespace Demo.App.Features.JsonColoring;

/// <inheritdoc />
public class JsonColorizer : IJsonColorizer
{
    /// <inheritdoc />
    public string ColorizeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var sb = new IndentedStringBuilder(0);
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
                sb.Append(SpanOpenBrace()).Append("<br>");
                sb.Indent();
                var properties = element.EnumerateObject().ToArray();
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    sb.AppendIndentation();
                    sb.Append($"{SpanJsonPropertyName(property.Name)}: ");
                    ProcessElement(property.Value, sb);
                    sb.Append(i < properties.Length - 1 ? ",<br>" : "<br>");
                }
                sb.Unindent();
                sb.AppendIndentation();
                sb.Append(SpanCloseBrace());
                break;

            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToArray();
                if (items.Length == 0)
                {
                    sb.Append(SpanOpenBracket()).Append(SpanCloseBracket());
                }
                else
                {
                    sb.Append(SpanOpenBracket()).Append("<br>");
                    sb.Indent();
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        ProcessElement(item, sb, true);
                        sb.Append(i < items.Length - 1 ? ",<br>" : "<br>");
                    }
                    sb.Unindent();
                    sb.AppendIndentation();
                    sb.Append(SpanCloseBracket());  
                }
                
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
}