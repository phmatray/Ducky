using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Ducky.Shared.Generators;

/// <summary>The <c>{Asm}</c> part of the GEN-02 registration names, from <c>Compilation.AssemblyName</c> (SPEC §16.1).</summary>
internal static class AssemblyNameSanitizer
{
    /// <summary>
    /// Maps every character that is invalid in a C# identifier to <c>_</c> and prefixes <c>_</c> to a leading non-start
    /// character (a digit) or a reserved keyword; a null or empty name is <c>Anonymous</c>. Names that differ only in such
    /// characters (<c>My.App</c>, <c>My-App</c>, <c>My_App</c>) collide, which is documented rather than hashed.
    /// </summary>
    public static string Sanitize(string? assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
        {
            return "Anonymous";
        }

        var name = new string(assemblyName!.Select(c => SyntaxFacts.IsIdentifierPartCharacter(c) ? c : '_').ToArray());
        return SyntaxFacts.IsIdentifierStartCharacter(name[0]) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "_" + name;
    }
}
