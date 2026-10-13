using System.Collections.Immutable;
using Ducky.Shared.Generators;

namespace Ducky.Draft.Generators.Tests;

// The shared generator helpers as linked into Ducky.Draft.Generators (SPEC §16.1): each linked copy is measured and
// mutated in its own assembly, so its contract is pinned here too.
public sealed class SharedHelpersTests
{
    private sealed record Model(string Name, EquatableArray<string> Members);

    [Fact]
    public void EquatableArray_ValueEquality()
    {
        var ab = new EquatableArray<string>(["a", "b"]);
        var ab2 = new EquatableArray<string>([.. "ab".Select(c => c.ToString())]);
        var empty = new EquatableArray<string>([]);

        ab.Equals(ab2).ShouldBeTrue();
        ab.Equals((object)ab2).ShouldBeTrue();
        ab.GetHashCode().ShouldBe(ab2.GetHashCode());
        new Model("M", ab).ShouldBe(new Model("M", ab2));

        ab.Equals(new EquatableArray<string>(["b", "a"])).ShouldBeFalse();
        ab.Equals(empty).ShouldBeFalse();
        ab.Equals((object)ImmutableArray.Create("a", "b")).ShouldBeFalse();
        ab.Equals(null).ShouldBeFalse();

        // default (a model field never assigned, or a default ImmutableArray) reads as empty.
        foreach (var unset in new[] { default, new EquatableArray<string>(default) })
        {
            unset.Items.ShouldBeEmpty();
            unset.Equals(empty).ShouldBeTrue();
            unset.GetHashCode().ShouldBe(empty.GetHashCode());
        }
    }
}
