using System.Globalization;
using System.Text.RegularExpressions;

namespace Ducky;

/// <summary>
/// The catalogue of Ducky's runtime codes (SPEC §8.2). Every message follows the template of §8.3 (INV-31): what
/// happened with the concrete types and keys, the exact fix, the link.
/// </summary>
internal static class DuckyErrors
{
    public static string Format(DuckyError error) => $"{error.Code}: {error.Message} {error.Fix} {error.HelpLink}";

    public static DuckyError AddDuckyTwice() => Create(
        "DUCKY300",
        "AddDucky was called twice on the same IServiceCollection.",
        "Call AddDucky once and register every slice, effect and middleware inside its configure callback.");

    public static DuckyError TransientLifetime() => Create(
        "DUCKY301",
        "DuckyBuilder.Lifetime is ServiceLifetime.Transient, which would give every resolution its own store.",
        "Set Lifetime to ServiceLifetime.Singleton or ServiceLifetime.Scoped, or leave it unset to get the host default.");

    public static DuckyError GenericSliceWithoutKey(Type sliceType) => Create(
        "DUCKY302",
        $"Slice {Display(sliceType)} is generic, so its key can't be derived from its type name.",
        $"Override Key in {Display(sliceType)} so that each closed generic type returns its own fixed key.");

    public static DuckyError InvalidKey(Type sliceType, string key) => Create(
        "DUCKY303",
        $"Slice {Display(sliceType)} has the key '{key}', which is invalid or reserved.",
        "Return a key that matches ^[a-z0-9]+(-[a-z0-9]+)*$ from Key; the @ducky/ prefix is reserved for Ducky's own packages.");

    public static DuckyError DuplicateKey(string key, Type firstSlice, Type secondSlice) => Create(
        "DUCKY304",
        $"Slices {Display(firstSlice)} and {Display(secondSlice)} both have the key '{key}'.",
        $"Override Key in {Display(secondSlice)} to return a key no other slice uses.");

    public static DuckyError DuplicateStateType(Type stateType, Type firstSlice, Type secondSlice) => Create(
        "DUCKY305",
        $"Slices {Display(firstSlice)} and {Display(secondSlice)} both hold state type {Display(stateType)}.",
        $"Give {Display(secondSlice)} its own state type, so that State.Get<{Display(stateType)}>() finds exactly one slice.");

    public static DuckyError MissingJsonTypeInfo(Type type, string requiredBy) => Create(
        "DUCKY306",
        $"{requiredBy} needs {Display(type)}, but the resolver passed to UseJson has no JsonTypeInfo for {Display(type)}.",
        $"Add [JsonSerializable(typeof({Display(type)}))] to your JsonSerializerContext.");

    public static DuckyError NullTypeInfoResolver() => Create(
        "DUCKY306",
        "UseJson received JsonSerializerOptions whose TypeInfoResolver is null.",
        "Set TypeInfoResolver on those options, or call UseJson with your JsonSerializerContext.Default instead.");

    public static DuckyError NonConcreteHandlerType(Type owner, Type actionType) => Create(
        "DUCKY307",
        $"{Display(owner)} calls On<{Display(actionType)}>(), but {Display(actionType)} is abstract, an interface or object.",
        $"Call On<T>() once per concrete action type instead of {Display(actionType)}: handlers match the exact runtime type.");

    public static DuckyError DuplicateHandler(Type owner, Type actionType) => Create(
        "DUCKY308",
        $"{Display(owner)} calls On<{Display(actionType)}>() more than once.",
        $"Keep one On<{Display(actionType)}>() call in {Display(owner)} and merge the handlers into it.");

    public static DuckyError UnresolvableConstructor(Type type, string requiredBy, Type service, object? serviceKey)
    {
        var key = Convert.ToString(serviceKey, CultureInfo.InvariantCulture);
        return Create(
            "DUCKY309",
            serviceKey is null
                ? $"{Display(type)} ({requiredBy}) can't be constructed: no constructor can resolve its {Display(service)} parameter."
                : $"{Display(type)} ({requiredBy}) can't be constructed: no constructor can resolve its {Display(service)} parameter with key '{key}'.",
            serviceKey is null
                ? $"Register {Display(service)} in the IServiceCollection, or give that parameter a default value."
                : $"Register {Display(service)} as a keyed service with key '{key}', or give that parameter a default value.");
    }

    public static DuckyError ServiceKeyParameter(Type type, string requiredBy, string parameterName) => Create(
        "DUCKY309",
        $"{Display(type)} ({requiredBy}) can't be constructed: its {parameterName} parameter is marked [ServiceKey], which never resolves.",
        $"Remove [ServiceKey] from {parameterName}, or give that parameter a default value: Ducky never creates keyed effects or middleware.");


    public static DuckyError UnregisteredState(Type stateType)
    {
        // ponytail: the generator's sanitizer (§16.1) also prefixes a leading digit or a C# keyword; a hint needs no more.
        var assembly = stateType.Assembly.GetName().Name!;
        return Create(
            "DUCKY350",
            $"No slice holds state type {Display(stateType)}.",
            $"Call AddDuckyGenerated_{Regex.Replace(assembly, @"\W", "_")}() inside AddDucky to register the slices of {assembly}, or register the slice with AddSlice<TSlice>().");
    }

    public static DuckyError SliceStoreNotAttached(Type sliceStoreType) => Create(
        "DUCKY351",
        $"{Display(sliceStoreType)} was used before a store attached it, so it was created with new.",
        $"Inject {Display(sliceStoreType)}, which AddSlice<{Display(sliceStoreType)}>() registers, instead of creating it with new.");

    public static DuckyError ConstructorThrew(Type type, Exception exception) => Create(
        "DUCKY353",
        exception is DuckyConfigurationException configuration
            ? $"The constructor of {Display(type)} threw {Display(exception.GetType())} ({string.Join(", ", configuration.Errors.Select(e => e.Code))}) when the store created its effects and middleware."
            : $"The constructor of {Display(type)} threw {Display(exception.GetType())} when the store created its effects and middleware.",
        $"Fix the constructor of {Display(type)} so that it does not throw; the inner exception has the details.");

    private static DuckyError Create(string code, string message, string fix) =>
        new(code, message, fix, $"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{code}.md");

    // C# spelling of a type: Outer.Inner, Name<Arg, Arg>, Element[,], and T for a generic parameter.
    // ponytail: a type nested in a generic type lists every generic argument at the end (Outer.Inner<T>).
    internal static string Display(Type type) => type.IsGenericType
        ? $"{Regex.Replace(type.GetGenericTypeDefinition().FullName!, @"`\d+", "").Replace('+', '.')}<{string.Join(", ", type.GetGenericArguments().Select(Display))}>"
        : type.IsArray ? $"{Display(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]"
        : type.IsGenericParameter ? type.Name
        : type.FullName!.Replace('+', '.');
}
