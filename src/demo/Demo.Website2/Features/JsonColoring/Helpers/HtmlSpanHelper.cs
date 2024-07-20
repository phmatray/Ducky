namespace Demo.Website2.Features.JsonColoring;

/// <summary>
/// Provides methods to generate HTML span elements with colors for JSON formatting.
/// </summary>
public static class HtmlSpanHelper
{
    private const string ColorBraces = Palette.Black;
    private const string ColorBrackets = Palette.Purple;
 
    private const string ColorQuote = Palette.BlueGrey;
    private const string ColorPropertyName = Palette.Pink;
    
    private const string ColorString = Palette.Green;
    private const string ColorNumber = Palette.Orange;
    private const string ColorBool = Palette.Blue;
    private const string ColorNull = Palette.Red;

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
        => SpanQuote() + Span(text, ColorString) + SpanQuote();

    /// <summary>
    /// Generates an HTML span element for a JSON number value.
    /// </summary>
    /// <param name="text">The JSON number value.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonNumber(string text)
        => Span(text, ColorNumber);

    /// <summary>
    /// Generates an HTML span element for a JSON boolean value.
    /// </summary>
    /// <param name="text">The JSON boolean value.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonBool(string text)
        => Span(text, ColorBool);

    /// <summary>
    /// Generates an HTML span element for a JSON null value.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonNull()
        => Span("null", ColorNull);

    /// <summary>
    /// Generates an HTML span element for an open brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanOpenBrace()
        => Span("{", ColorBraces);

    /// <summary>
    /// Generates an HTML span element for a close brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanCloseBrace()
        => Span("}", ColorBraces);

    /// <summary>
    /// Generates an HTML span element for a close brace.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanOpenBracket()
        => Span("[", ColorBrackets);

    /// <summary>
    /// Generates an HTML span element for a close bracket.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanCloseBracket()
        => Span("]", ColorBrackets);

    // SpanQuote
    /// <summary>
    /// Generates an HTML span element for a quote.
    /// </summary>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanQuote()
        => Span("&quot;", ColorQuote);

    /// <summary>
    /// Generates an HTML span element for a JSON property name.
    /// </summary>
    /// <param name="name">The JSON property name.</param>
    /// <returns>An HTML span element as a string.</returns>
    public static string SpanJsonPropertyName(string name)
        => SpanQuote() + Span(name, ColorPropertyName) + SpanQuote();
}