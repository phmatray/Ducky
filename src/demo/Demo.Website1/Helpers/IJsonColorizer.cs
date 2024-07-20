namespace Demo.Website1.Helpers;

/// <summary>
/// Defines a method to colorize a JSON string to an HTML string with color coding.
/// </summary>
public interface IJsonColorizer
{
    /// <summary>
    /// Colorizes a JSON string to an HTML string with color coding.
    /// </summary>
    /// <param name="json">The JSON string to colorize.</param>
    /// <returns>An HTML string with color-coded JSON.</returns>
    string ColorizeJson(string json);
}
