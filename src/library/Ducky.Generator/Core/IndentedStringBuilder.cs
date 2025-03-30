using System.Text;

namespace Ducky.Generator.Core;

/// <summary>
/// Helps generate formatted code with proper indentation.
/// </summary>
public class IndentedStringBuilder
{
    private readonly StringBuilder _builder;
    private int _indentLevel;
    private readonly string _indentString;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndentedStringBuilder"/> class.
    /// </summary>
    /// <param name="indentString">The string to use for one level of indentation (default is four spaces).</param>
    public IndentedStringBuilder(string indentString = "    ")
    {
        _builder = new StringBuilder();
        _indentString = indentString;
    }

    /// <summary>
    /// Increases the indentation level.
    /// </summary>
    public void Indent()
    {
        _indentLevel++;
    }

    /// <summary>
    /// Decreases the indentation level.
    /// </summary>
    public void Outdent()
    {
        if (_indentLevel <= 0)
        {
            return;
        }

        _indentLevel--;
    }

    /// <summary>
    /// Appends a line with the current indentation.
    /// </summary>
    /// <param name="line">The line to append.</param>
    public IndentedStringBuilder AppendLine(string line = "")
    {
        if (!string.IsNullOrEmpty(line))
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _builder.Append(_indentString);
            }

            _builder.AppendLine(line);
        }
        else
        {
            _builder.AppendLine();
        }

        return this;
    }

    /// <summary>
    /// Appends text without a newline.
    /// </summary>
    /// <param name="text">The text to append.</param>
    public IndentedStringBuilder Append(string text)
    {
        _builder.Append(text);
        return this;
    }

    /// <summary>
    /// Gets the generated string.
    /// </summary>
    public override string ToString()
    {
        return _builder.ToString();
    }
}
