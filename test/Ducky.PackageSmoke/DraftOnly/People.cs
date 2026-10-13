using Ducky.Draft;

namespace Ducky.PackageSmoke.DraftOnly;

/// <summary>A nested draftable record.</summary>
/// <param name="City">The city.</param>
[Draftable]
public sealed partial record Address(string City);

/// <summary>A draftable record with a nested draftable member.</summary>
/// <param name="Name">The name.</param>
/// <param name="Home">The home address.</param>
[Draftable]
public sealed partial record Person(string Name, Address Home);

/// <summary>Edits through the generated drafts.</summary>
public static class Edits
{
    /// <summary>Edits the nested member inside a recipe.</summary>
    /// <param name="person">The person to edit.</param>
    /// <returns>The edited person.</returns>
    public static Person Move(Person person) => person.Produce(d => d.Home.City = "Paris");

    /// <summary>Edits through an explicit draft.</summary>
    /// <param name="person">The person to edit.</param>
    /// <returns>The edited person.</returns>
    public static Person Rename(Person person)
    {
        var draft = person.CreateDraft();
        draft.Name = "Ada";
        return draft.Build();
    }
}
