using System.Collections.Immutable;

namespace Ducky.Draft.Tests.Properties;

// The structural oracle of SPEC §17.7: a recursive element-wise comparison, never record Equals (synthesized equality
// compares collection members by reference, so a correct Produce would never equal the `with` result once a collection
// changed). It also asserts the identity of dictionary KeyComparer/ValueComparer and set comparers. A mismatch names its path.
internal static class Structural
{
    public static void ShouldMatch(Root actual, Root expected)
    {
        actual.Count.ShouldBe(expected.Count, "Count");
        actual.Name.ShouldBe(expected.Name, "Name");
        Same(actual.Main, expected.Main, "Main");
        Same(actual.Spare, expected.Spare, "Spare");
        List(actual.Items, expected.Items, "Items", Same);
        Array(actual.Cards, expected.Cards, "Cards", Same);
        Array(actual.Numbers, expected.Numbers, "Numbers", Scalar);
        List(actual.Holes, expected.Holes, "Holes", Same);
        Dictionary(actual.ById, expected.ById, "ById", Same);
        Dictionary(actual.Counts, expected.Counts, "Counts", Scalar);
        actual.Tags.KeyComparer.ShouldBeSameAs(expected.Tags.KeyComparer, "Tags.KeyComparer");
        List(Ordinal(actual.Tags), Ordinal(expected.Tags), "Tags", Scalar);
        actual.Ranks.KeyComparer.ShouldBeSameAs(expected.Ranks.KeyComparer, "Ranks.KeyComparer");
        List(actual.Ranks, expected.Ranks, "Ranks", Scalar);
        if (expected.Names is null)
        {
            actual.Names.ShouldBeNull("Names");
        }
        else
        {
            actual.Names.ShouldNotBeNull("Names");
            List(actual.Names, expected.Names, "Names", Scalar);
        }
    }

    private static void Same(Item? actual, Item? expected, string path)
    {
        if (expected is null)
        {
            actual.ShouldBeNull(path);
            return;
        }

        actual.ShouldNotBeNull(path);
        actual.Id.ShouldBe(expected.Id, path + ".Id");
        actual.Title.ShouldBe(expected.Title, path + ".Title");
        Same(actual.Label, expected.Label, path + ".Label");
        Same(actual.Extra, expected.Extra, path + ".Extra");
    }

    private static void Same(Tag? actual, Tag? expected, string path)
    {
        if (expected is null)
        {
            actual.ShouldBeNull(path);
            return;
        }

        actual.ShouldNotBeNull(path);
        actual.Name.ShouldBe(expected.Name, path + ".Name");
    }

    private static void Scalar<T>(T actual, T expected, string path) => actual.ShouldBe(expected, path);

    private static void List<T>(IReadOnlyList<T> actual, IReadOnlyList<T> expected, string path, Action<T, T, string> element)
    {
        actual.Count.ShouldBe(expected.Count, path + ".Count");
        for (var i = 0; i < expected.Count; i++)
        {
            element(actual[i], expected[i], $"{path}[{i}]");
        }
    }

    private static void Array<T>(ImmutableArray<T> actual, ImmutableArray<T> expected, string path, Action<T, T, string> element)
    {
        actual.IsDefault.ShouldBe(expected.IsDefault, path + ".IsDefault");
        if (!expected.IsDefault)
        {
            List(actual, expected, path, element);
        }
    }

    private static void Dictionary<TValue>(
        ImmutableDictionary<string, TValue> actual, ImmutableDictionary<string, TValue> expected, string path, Action<TValue, TValue, string> value)
    {
        actual.KeyComparer.ShouldBeSameAs(expected.KeyComparer, path + ".KeyComparer");
        actual.ValueComparer.ShouldBeSameAs(expected.ValueComparer, path + ".ValueComparer");
        List(Ordinal(actual.Keys), Ordinal(expected.Keys), path + ".Keys", Scalar);
        foreach (var key in expected.Keys)
        {
            value(actual[key], expected[key], $"{path}[{key}]");
        }
    }

    private static List<string> Ordinal(IEnumerable<string> keys) => [.. keys.Order(StringComparer.Ordinal)];
}
