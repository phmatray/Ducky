// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Tests.Core;

public class ValueCollectionTests
{
    [Fact]
    public void OrderItem_Should_BeEquals()
    {
        // Arrange
        OrderItem item1 = new("ProductA", 2, 10.0m);
        OrderItem item2 = new("ProductA", 2, 10.0m);

        // Assert
        item1.ProductName.Should().Be(item2.ProductName);
        item1.Quantity.Should().Be(item2.Quantity);
        item1.Price.Should().Be(item2.Price);
        item1.Should().Be(item2);
    }

    [Fact]
    public void Order_Should_BeEquals()
    {
        // Arrange
        OrderItem item1 = new("ProductA", 2, 10.0m);
        OrderItem item2 = new("ProductB", 1, 20.0m);

        Order order1 = new("Order1", "John Doe", [item1, item2]);
        Order order2 = new("Order1", "John Doe", [item1, item2]);

        // Assert
        order1.OrderId.Should().Be(order2.OrderId);
        order1.CustomerName.Should().Be(order2.CustomerName);
        order1.Items.Should().HaveElementAt(0, order2.Items[0]);
        order1.Items.Should().HaveElementAt(1, order2.Items[1]);
        order1.Should().Be(order2);
    }

    [Fact]
    public void ValueCollection_Should_Add()
    {
        ValueCollection<string> collection =
        [
            "One",
            "Two"
        ];

        // Act
        collection.Add("Three");

        collection.Should().HaveCount(3);
        collection[0].Should().Be("One");
        collection[1].Should().Be("Two");
        collection[2].Should().Be("Three");
    }

    [Fact]
    public void ValueCollection_Should_ToString()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three"];
        const string expected = "[ One, Two, Three ]";

        // Act
        string result = collection.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValueCollection_Should_ToString_WithMoreThan3Items()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three", "Four"];
        const string expected = "[ One, Two, Three, ... ]";

        // Act
        string result = collection.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValueCollection_Should_BeEquals()
    {
        // Arrange
        ValueCollection<string> collection1 = ["One", "Two", "Three"];
        ValueCollection<string> collection2 = ["One", "Two", "Three"];

        // Assert
        collection1.Should().BeEquivalentTo(collection2);
    }

    [Fact]
    public void ValueCollection_Should_NotBeEquals()
    {
        // Arrange
        ValueCollection<string> collection1 = ["One", "Two", "Three"];
        ValueCollection<string> collection2 = ["One", "Two", "Four"];

        // Assert
        collection1.Should().NotEqual(collection2);
    }

    [Fact]
    public void ValueCollection_Should_NotBeEquals_WhenNull()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three"];

        // Act
        bool equals = collection.Equals(null);

        // Assert
        equals.Should().BeFalse();
    }

    [Fact]
    public void ValueCollection_Should_HaveHashCode()
    {
        // Arrange
        ValueCollection<string> collection1 = ["One", "Two", "Three"];
        ValueCollection<string> collection2 = ["One", "Two", "Three"];

        // Act
        int hashCode1 = collection1.GetHashCode();
        int hashCode2 = collection2.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void ValueCollection_Should_ThrowArgumentNullException()
    {
        // Arrange
        ValueCollection<string> collection = [];

        // Act
        Action act = () => collection.Add(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValueCollection_Should_ImplicitConvertFromArray()
    {
        string[] items = ["One", "Two", "Three"];
        ValueCollection<string> collection = items;

        collection.Should().HaveCount(3);
        collection[0].Should().Be("One");
        collection[1].Should().Be("Two");
        collection[2].Should().Be("Three");
    }

    [Fact]
    public void ValueCollection_Should_ImplicitConvertFromList()
    {
        List<string> items = ["One", "Two", "Three"];
        ValueCollection<string> collection = items;

        collection.Should().HaveCount(3);
        collection[0].Should().Be("One");
        collection[1].Should().Be("Two");
        collection[2].Should().Be("Three");
    }

    [Fact]
    public void ValueCollection_Should_ProvideEnumerator()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three"];

        // Act
        IEnumerator<string> enumerator = collection.GetEnumerator();

        // Assert
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be("One");

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be("Two");

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be("Three");

        enumerator.MoveNext().Should().BeFalse();
        enumerator.Dispose();
    }

    [Fact]
    public void ValueCollection_Should_Clone()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three"];

        // Act
        var clone = (ValueCollection<string>)collection.Clone();

        // Assert
        collection.Should().BeEquivalentTo(clone);
        collection.GetHashCode().Should().Be(clone.GetHashCode());
    }

    [Fact]
    public void ValueCollection_Should_HaveIndexer()
    {
        // Arrange
        ValueCollection<string> collection = ["One", "Two", "Three"];

        // Act
        string first = collection[0];
        string second = collection[1];
        string third = collection[2];

        // Assert
        first.Should().Be("One");
        second.Should().Be("Two");
        third.Should().Be("Three");
    }
}
