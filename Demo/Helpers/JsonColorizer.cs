using System.Text;
using System.Text.Json;

namespace Demo.Helpers;

public static class JsonColorizer
{
    public static string ColorizeJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var processElement = ProcessElement(document.RootElement, 0);
        return processElement;
    }

    private static string ProcessElement(JsonElement element, int depth, bool inArray = false)
    {
        StringBuilder sb = new();
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (inArray)
                {
                    sb.Append(new string(' ', depth * 2));
                }
                sb.Append(SpanOpenBrace()).Append("<br>");
                depth++;
                var properties = element.EnumerateObject().ToArray();
                for (int i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    sb.Append(new string(' ', depth * 2));
                    sb.Append($"{SpanJsonPropertyName(property.Name)}: {ProcessElement(property.Value, depth)}");
                    sb.Append(i < properties.Length - 1 ? ",<br>" : "<br>");
                }
                depth--;
                sb.Append(new string(' ', depth * 2));
                sb.Append(SpanCloseBrace());
                break;

            case JsonValueKind.Array:
                sb.Append(SpanOpenBracket()).Append("<br>");
                depth++;
                var items = element.EnumerateArray().ToArray();
                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    sb.Append($"{ProcessElement(item, depth, true)}");
                    sb.Append(i < items.Length - 1 ? ",<br>" : "<br>");
                }
                depth--;
                sb.Append(new string(' ', depth * 2));
                sb.Append(SpanCloseBracket());
                break;

            case JsonValueKind.String:
                sb.Append(SpanJsonString(element));
                break;

            case JsonValueKind.Number:
                sb.Append(SpanJsonNumber(element));
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append(SpanJsonBool(element));
                break;

            case JsonValueKind.Null:
                sb.Append(SpanJsonNull());
                break;
        }

        return sb.ToString();
    }
    
    private static string Span(string text, string color)
        => $"<span style='color: {color};'>{text}</span>";
    
    private static string SpanJsonString(JsonElement element)
        => Span($"&quot;{element.GetString()}&quot;", "red");
    
    private static string SpanJsonNumber(JsonElement element)
        => Span(element.GetRawText(), "blue");
    
    private static string SpanJsonBool(JsonElement element)
        => Span(element.GetRawText(), "purple"); 
    
    private static string SpanJsonNull()
        => Span("null", "gray");
    
    private static string SpanOpenBrace()
        => Span("{", "brown");
    
    private static string SpanCloseBrace()
        => Span("}", "brown");
    
    private static string SpanOpenBracket()
        => Span("[", "brown");
    
    private static string SpanCloseBracket()
        => Span("]", "brown");

    private static string SpanJsonPropertyName(string name)
        => Span($"&quot;{name}&quot;", "green");
}