using System.Text;

namespace Demo.Helpers;

/// <summary>
/// Provides a helper class to build indented strings using a <see cref="StringBuilder"/>.
/// </summary>
/// <param name="indentLevel">The initial indentation level.</param>
/// <param name="indentSize">The number of spaces per indentation level. Default is 2.</param>
public sealed class IndentedStringBuilder(int indentLevel, int indentSize = 2)
{
    private readonly StringBuilder _sb = new();

    /// <summary>
    /// Appends the specified string value to the current string.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder Append(string value)
    {
        _sb.Append(value);
        return this;
    }

    /// <summary>
    /// Appends the specified string value followed by a line terminator to the current string,
    /// with indentation based on the current indentation level.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder AppendLine(string value)
    {
        _sb.Append(new string(' ', indentLevel * indentSize));
        _sb.AppendLine(value);
        return this;
    }
    
    /// <summary>
    /// Appends the current indentation to the current string.
    /// </summary>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder AppendIndentation()
    {
        _sb.Append(new string(' ', indentLevel * indentSize));
        return this;
    }

    /// <summary>
    /// Increases the indentation level by one.
    /// </summary>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder Indent()
    {
        indentLevel++;
        return this;
    }

    /// <summary>
    /// Decreases the indentation level by one if it is greater than zero.
    /// </summary>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder Unindent()
    {
        if (indentLevel > 0)
        {
            indentLevel--;
        }

        return this;
    }

    /// <summary>
    /// Clears the current string.
    /// </summary>
    /// <returns>The current instance of <see cref="IndentedStringBuilder"/>.</returns>
    public IndentedStringBuilder Clear()
    {
        _sb.Clear();
        return this;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return _sb.ToString();
    }
}