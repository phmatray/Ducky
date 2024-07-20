// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace Demo.Website1.Helpers;

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
    public void Append(string value)
    {
        _sb.Append(value);
    }

    /// <summary>
    /// Appends the specified string value followed by a line terminator to the current string,
    /// with indentation based on the current indentation level.
    /// </summary>
    /// <param name="value">The string to append.</param>
    public void AppendLine(string value)
    {
        _sb.AppendLine(new string(' ', indentLevel * indentSize) + value);
    }

    /// <summary>
    /// Appends the current indentation to the current string.
    /// </summary>
    public void AppendIndentation()
    {
        _sb.Append(new string(' ', indentLevel * indentSize));
    }

    /// <summary>
    /// Increases the indentation level by one.
    /// </summary>
    public void Indent()
    {
        indentLevel++;
    }

    /// <summary>
    /// Decreases the indentation level by one if it is greater than zero.
    /// </summary>
    public void Unindent()
    {
        if (indentLevel > 0)
        {
            indentLevel--;
        }
    }

    /// <summary>
    /// Clears the current string.
    /// </summary>
    public void Clear()
    {
        _sb.Clear();
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
