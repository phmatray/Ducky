using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ducky.Shared.Generators;

/// <summary>
/// An <see cref="ImmutableArray{T}"/> compared by its elements, for the symbol-free pipeline models (GEN-06):
/// <see cref="ImmutableArray{T}"/> itself compares by reference, which would defeat incremental caching.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    /// <summary>Gets the elements; a default array (or a default <see cref="EquatableArray{T}"/>) reads as empty.</summary>
    public ImmutableArray<T> Items => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public bool Equals(EquatableArray<T> other) => Items.SequenceEqual(other.Items);

    // No pattern variable (CS0165) and a `!` (CS8605 under TreatWarningsAsErrors): a `&&` -> `||` mutant must still
    // compile, or Stryker Safe Mode hides the method.
    public override bool Equals(object? obj) => obj is EquatableArray<T> && Equals((EquatableArray<T>)obj!);

    public override int GetHashCode() => ((IStructuralEquatable)Items).GetHashCode(EqualityComparer<T>.Default);
}
