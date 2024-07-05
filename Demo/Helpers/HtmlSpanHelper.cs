namespace Demo.Helpers;

/// <summary>
/// Provides methods to generate HTML span elements with colors for JSON formatting.
/// </summary>
public static class HtmlSpanHelper
{
    private const string ColorBlue = "blue";
    private const string ColorBrown = "brown";
    private const string ColorGreen = "green";
    private const string ColorGray = "gray";
    private const string ColorPurple = "purple";
    private const string ColorRed = "red";
    
    /// <summary>
    /// Generates an HTML span element with the specified text and color.
    /// </summary>
    /// <param name="text">The text to be enclosed in the span.</param>
    /// <param name="color">The color of the text.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string Span(string text, string color)
        => $"<span style='color: {color};'>{text}</span>";

    /// <summary>
    /// Generates an HTML span element for a JSON string value.
    /// </summary>
    /// <param name="text">The JSON string value.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonString(string text)
        => Span($"&quot;{text}&quot;", ColorRed);

    /// <summary>
    /// Generates an HTML span element for a JSON number value.
    /// </summary>
    /// <param name="text">The JSON number value.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonNumber(string text)
        => Span(text, ColorBlue);

    /// <summary>
    /// Generates an HTML span element for a JSON boolean value.
    /// </summary>
    /// <param name="text">The JSON boolean value.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonBool(string text)
        => Span(text, ColorPurple);

    /// <summary>
    /// Generates an HTML span element for a JSON null value.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonNull()
        => Span("null", ColorGray);

    /// <summary>
    /// Generates an HTML span element for an open brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanOpenBrace()
        => Span("{", ColorBrown);

    /// <summary>
    /// Generates an HTML span element for a close brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanCloseBrace()
        => Span("}", ColorBrown);

    /// <summary>
    /// Generates an HTML span element for a close brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanOpenBracket()
        => Span("[", ColorBrown);

    /// <summary>
    /// Generates an HTML span element for a close bracket.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanCloseBracket()
        => Span("]", ColorBrown);

    /// <summary>
    /// Generates an HTML span element for a JSON property name.
    /// </summary>
    /// <param name="name">The JSON property name.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonPropertyName(string name)
        => Span($"&quot;{name}&quot;", ColorGreen);
}