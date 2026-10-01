using System.Reflection;
using System.Text.RegularExpressions;

namespace Ducky;

// SPEC §6.1: the default slice key and the rule an overridden key must satisfy (DUCKY303).
internal static partial class SliceKey
{
    private const string ReservedPrefix = "@ducky/";

    // Type name, prefixed by its containing types' names joined with '-'; one Slice/Store/Reducers/Reducer suffix
    // stripped when a name remains before it (so `Cart.Slice` gives `cart` and a type named `Reducers` keeps its name);
    // then kebab-cased, an acronym staying one word (`UIStateSlice` gives `ui-state`). Generic slices override Key
    // (DUCKY302), so a generic arity suffix never reaches this.
    internal static string FromType(Type type)
    {
        var name = type.Name;
        for (var outer = type.DeclaringType; outer is not null; outer = outer.DeclaringType)
        {
            name = outer.Name + "-" + name;
        }

        foreach (var suffix in (ReadOnlySpan<string>)["Slice", "Store", "Reducers", "Reducer"])
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name[..^suffix.Length].TrimEnd('-');
                break;
            }
        }

        return WordBoundary().Replace(name, "-").ToLowerInvariant();
    }

    // `\z`, the end of the string, as SPEC §6.1 reads the regex's `$` (.NET's `$` also accepts a final "\n"). The @ducky/
    // prefix is reserved for assemblies signed with Ducky.dll's key (anti-accident only: the key is checked in, SPEC §18).
    internal static bool IsValid(string key, Assembly sliceAssembly) =>
        KeyPattern().IsMatch(key)
        && (!key.StartsWith(ReservedPrefix, StringComparison.Ordinal)
            || sliceAssembly.GetName().GetPublicKeyToken().AsSpan()
                .SequenceEqual(typeof(SliceKey).Assembly.GetName().GetPublicKeyToken()));

    // Before an uppercase letter that follows a lowercase letter or digit, and before the last capital of an acronym
    // that starts a new word (`HTTPClient` gives `HTTP-Client`).
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();

    [GeneratedRegex(@"^(@ducky/)?[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
