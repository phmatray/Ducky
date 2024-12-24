using System.Collections.ObjectModel;
using System.Text;

namespace Ducky;

/// <summary>
/// Represents a collection of values.
/// </summary>
/// <param name="values">The values to initialize the collection with.</param>
/// <typeparam name="T">The type of the values in the collection.</typeparam>
public class ValueCollection<T>(params IEnumerable<T> values)
    : ReadOnlyCollection<T>(new List<T>(values)), IEquatable<ValueCollection<T>>, ICloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueCollection{T}"/> class.
    /// </summary>
    public bool IsEmpty
        => Items.Count == 0;

    /// <summary>
    /// Indexer to access items in the collection.
    /// </summary>
    /// <param name="index">The index of the item to get or set.</param>
    /// <returns>The item at the specified index.</returns>
    public T this[Index index]
    {
        get => Items[index];

        set => Items[index] = value;
    }

    /// <summary>
    /// Adds an item to the collection.
    /// </summary>
    /// <param name="item">The object to add to the value collection.</param>
    /// <returns>The current value collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when item is null.</exception>
    public ValueCollection<T> Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Items.Add(item);

        return this;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// The comparison is done by comparing the items in the collection.
    /// </summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is ValueCollection<T> other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified ValueCollection is equal to the current ValueCollection by comparing the items.
    /// </summary>
    /// <param name="other">The ValueCollection to compare with the current ValueCollection.</param>
    /// <returns>true if the specified ValueCollection is equal to the current ValueCollection; otherwise, false.</returns>
    public bool Equals(ValueCollection<T>? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Items.SequenceEqual(other.Items);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hashCode = new();
        foreach (T item in Items)
        {
            hashCode.Add(item);
        }

        return hashCode.ToHashCode();
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// If the collection has more than 3 items, only the first 3 items are shown.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        sb.Append("[ ");

        // if we have more than 3 items, we only show the first 3 then "..."
        if (Items.Count > 3)
        {
            sb.AppendJoin(", ", Items.Take(3));
            sb.Append(", ...");
        }
        else
        {
            sb.AppendJoin(", ", Items);
        }

        sb.Append(" ]");

        return sb.ToString();
    }

    /// <inheritdoc />
    public object Clone()
    {
        return new ValueCollection<T>(Items);
    }

    /// <summary>
    /// Implicitly converts an array of items into a ValueCollection.
    /// </summary>
    /// <param name="items">The items to convert.</param>
    public static implicit operator ValueCollection<T>(T[] items)
    {
        return new(items);
    }

    /// <summary>
    /// Implicitly converts a list of items into a ValueCollection.
    /// </summary>
    /// <param name="items">The items to convert.</param>
    public static implicit operator ValueCollection<T>(List<T> items)
    {
        return new(items);
    }
}
