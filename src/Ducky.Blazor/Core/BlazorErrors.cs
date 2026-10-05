using System.Text.RegularExpressions;

namespace Ducky.Blazor;

/// <summary>
/// The catalogue of Ducky.Blazor's runtime codes (SPEC §8.2). Every message follows the template of §8.3 (INV-31): what
/// happened with the concrete types and keys, the exact fix, the link.
/// </summary>
internal static class BlazorErrors
{
    public static string Format(DuckyError error) => $"{error.Code}: {error.Message} {error.Fix} {error.HelpLink}";

    public static DuckyError SelectAfterFirstRender(Type componentType) => Create(
        "DUCKY352",
        $"Select was called on {Display(componentType)} after its first render, when its selections are already registered.",
        $"Call Select in OnInitialized of {Display(componentType)} and keep the Selection<T> in a field; a selector may read parameters, so a parameter change needs no new Select.");

    private static DuckyError Create(string code, string message, string fix) =>
        new(code, message, fix, $"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{code}.md");

    // C# spelling of a closed type: Outer.Inner, Name<Arg, Arg> and Element[,] (a copy of the core's DuckyErrors.Display,
    // which is internal to Ducky).
    // ponytail: no open generics (a component's runtime type is closed), and a type nested in a generic type lists every
    // generic argument at the end (Outer.Inner<T>), like the core's.
    private static string Display(Type type) => type.IsGenericType
        ? $"{Regex.Replace(type.GetGenericTypeDefinition().FullName!, @"`\d+", "").Replace('+', '.')}<{string.Join(", ", type.GetGenericArguments().Select(Display))}>"
        : type.IsArray ? $"{Display(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]"
        : type.FullName!.Replace('+', '.');
}
