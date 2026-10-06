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

    public static DuckyError SliceNotAdded(string call, Type slice) => Create(
        "DUCKY310",
        $"{call}<{Display(slice)}> was called, but {Display(slice)} was never added to the store.",
        $"Call AddSlice<{Display(slice)}>() in AddDucky, or remove the {call}<{Display(slice)}> call.");

    public static DuckyError MigrationGap(Type slice, int version, IEnumerable<int> missing) => Create(
        "DUCKY311",
        $"{Display(slice)} is persisted with Version = {version}, but its migration chain has no step from version {string.Join(", ", missing)}.",
        $"Add {string.Join(" and ", missing.Select(static from => $"Migrate({from}, …)"))} to Persist<{Display(slice)}>, so a stored state of every older version reaches version {version}.");

    public static DuckyError SyncAcrossTabsWithoutLocal(Type slice, PersistStorage storage) => Create(
        "DUCKY312",
        $"{Display(slice)} sets SyncAcrossTabs with PersistStorage.{storage}, but tabs are kept in sync only with PersistStorage.Local.",
        $"Set Storage = PersistStorage.Local in Persist<{Display(slice)}>, or turn SyncAcrossTabs off.");

    public static DuckyError HydrationTimeoutNotBelowInitTimeout(TimeSpan hydrationTimeout, TimeSpan initTimeout) => Create(
        "DUCKY316",
        $"BlazorOptions.HydrationTimeout ({hydrationTimeout:c}) is not shorter than DuckyBuilder.InitTimeout ({initTimeout:c}), so init would abort before hydration times out.",
        "Set BlazorOptions.HydrationTimeout below DuckyBuilder.InitTimeout, or raise InitTimeout.");

    public static DuckyError SeedWaitNotBelowHydrationTimeout(TimeSpan seedWait, TimeSpan hydrationTimeout) => Create(
        "DUCKY316",
        $"BlazorOptions.PrerenderSeedWaitTimeout ({seedWait:c}) is not shorter than BlazorOptions.HydrationTimeout ({hydrationTimeout:c}), so hydration would time out while it waits for the prerender seed.",
        "Set BlazorOptions.PrerenderSeedWaitTimeout below BlazorOptions.HydrationTimeout, or raise HydrationTimeout.");

    private static DuckyError Create(string code, string message, string fix) =>
        new(code, message, fix, $"https://github.com/phmatray/Ducky/blob/main/docs/diagnostics/{code}.md");

    // C# spelling of a closed type: Outer.Inner, Name<Arg, Arg> and Element[,] (a copy of the core's DuckyErrors.Display,
    // which is internal to Ducky).
    // ponytail: no open generics (a component's runtime type is closed), and a type nested in a generic type lists every
    // generic argument at the end (Outer.Inner<T>), like the core's.
    internal static string Display(Type type) => type.IsGenericType
        ? $"{Regex.Replace(type.GetGenericTypeDefinition().FullName!, @"`\d+", "").Replace('+', '.')}<{string.Join(", ", type.GetGenericArguments().Select(Display))}>"
        : type.IsArray ? $"{Display(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]"
        : type.FullName!.Replace('+', '.');
}
