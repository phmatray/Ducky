using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace Ducky;

// SPEC §6.2: built once at store build and never changed; a slice's ordinal is its registration index. The builder rejects
// duplicate keys and state types (DUCKY304, DUCKY305) before it builds the registry.
internal sealed class Registry
{
    internal Registry(Slice[] slices)
    {
        Slices = slices;
        Keys = Array.AsReadOnly(Array.ConvertAll(slices, slice => slice.Key));
        ByKey = Keys.Index().ToFrozenDictionary(entry => entry.Item, entry => entry.Index);
        ByStateType = slices.Index().ToFrozenDictionary(entry => entry.Item.StateType, entry => entry.Index);
    }

    internal Slice[] Slices { get; }

    internal ReadOnlyCollection<string> Keys { get; }

    internal FrozenDictionary<string, int> ByKey { get; }

    internal FrozenDictionary<Type, int> ByStateType { get; }
}
