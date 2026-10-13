# DUCKY105: `[Draftable]` record member of a mutable collection type

**Package:** Ducky.Draft (generator `Ducky.Draft.Generators`). **Surface:** build warning on the member (SPEC §13.3).

## What happened

A member of the record is a `List<T>`, an array (`T[]`, `T[,]`, ...) or a `Dictionary<TKey, TValue>`. The draft can't
copy such a collection on write or preserve its identity, so the member is a plain member of the draft (a get/set, or a
get-only one when the record can't set it): assigning replaces it wholesale, and mutating it in place (`d.Items.Add(x)`)
mutates the collection the original record still holds, which breaks immutability and the no-op guarantee of `Produce`
(INV-26). A member is reported once: an inherited one by the `[Draftable]` base of this compilation that declares it, and
otherwise (a base that gets no draft, or a base in another assembly) on the deriving record's name.

## How to fix it

Use the immutable counterpart, which the draft maps to a copy-on-write collection draft: `ImmutableList<T>` or
`ImmutableArray<T>` (`ListDraft<T>`), `ImmutableDictionary<TKey, TValue>` (`DictionaryDraft<TKey, TValue>`),
`ImmutableHashSet<T>` or `ImmutableSortedSet<T>` (`SetDraft<T>`). If the member must stay mutable, only ever replace it in
a recipe (`d.Items = [.. d.Items, x]`) and suppress the warning for that member.
