// The profile is a nested record edited with Ducky.Draft: a reducer writes through a
// draft, and Produce returns a new Profile that keeps every untouched branch as is.
using Ducky.Draft;

namespace Ducky.Playground.Shop.Account;

// [Draftable] generates a copy-on-write draft for each record of the tree.
[Draftable]
public sealed partial record Profile(
    string Name,
    string Email,
    Address Address,
    Delivery Delivery)
{
    public string DeliverTo =>
        $"{Name}, {Address.Street}, {Address.Postcode} {Address.City}";
}

[Draftable]
public sealed partial record Address(
    string Street,
    string City,
    string Postcode,
    string Country);

[Draftable]
public sealed partial record Delivery(bool LeaveWithNeighbour, string Instructions);

public enum ProfileField
{
    Name,
    Email,
    Street,
    City,
    Postcode,
    Country,
    Instructions,
}

[DuckyAction]
[ActionType("profile/edited")]
public sealed record EditProfile(ProfileField Field, string Value);

[DuckyAction]
[ActionType("profile/neighbour-set")]
public sealed record SetLeaveWithNeighbour(bool Allowed);

public sealed class ProfileSlice : Slice<Profile>
{
    public ProfileSlice()
    {
        // Produce returns the original instance when the recipe changes nothing (the same
        // value typed again), so the store commits nothing and no component re-renders.
        On<EditProfile>((state, action) => state.Produce(draft =>
        {
            var value = action.Value.Trim();
            switch (action.Field)
            {
                case ProfileField.Name:
                    draft.Name = value;
                    break;
                case ProfileField.Email:
                    draft.Email = value;
                    break;
                case ProfileField.Street:
                    draft.Address.Street = value;
                    break;
                case ProfileField.City:
                    draft.Address.City = value;
                    break;
                case ProfileField.Postcode:
                    draft.Address.Postcode = value;
                    break;
                case ProfileField.Country:
                    draft.Address.Country = value;
                    break;
                case ProfileField.Instructions:
                    draft.Delivery.Instructions = value;
                    break;
            }
        }));
        On<SetLeaveWithNeighbour>((state, action) =>
            state.Produce(draft => draft.Delivery.LeaveWithNeighbour = action.Allowed));
    }

    public override string Key => "profile";

    protected override Profile Initial => new(
        "Ada Lovelace",
        "ada@example.com",
        new Address("12 Analytical Row", "London", "N1 9GU", "United Kingdom"),
        new Delivery(LeaveWithNeighbour: true, Instructions: ""));
}
