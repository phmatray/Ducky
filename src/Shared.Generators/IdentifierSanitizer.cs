using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Shared.Generators;

/// <summary>Identifiers and hint names for generated code (SPEC §16.1).</summary>
internal static class IdentifierSanitizer
{
    /// <summary>Returns <paramref name="name"/> verbatim, with a reserved C# keyword escaped by <c>@</c>.</summary>
    public static string EscapeKeyword(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    /// <summary>
    /// Turns every character outside <c>[A-Za-z0-9_.]</c> of a fully qualified metadata name into <c>_</c> (<c>+</c> of
    /// nested names, <c>@</c>, <c>&lt;&gt;</c>, <c>`</c>, non-ASCII), so the hint name is always valid (CS8785). It is not
    /// unique: <c>N.Outer+Inner</c> and <c>N.Outer_Inner</c> collide, and <c>AddSource</c> also rejects names equal under
    /// <see cref="System.StringComparison.OrdinalIgnoreCase"/>, so callers deduplicate (for example with a counter suffix).
    /// </summary>
    public static string HintName(string metadataName) => Regex.Replace(metadataName, "[^A-Za-z0-9_.]", "_");
}
