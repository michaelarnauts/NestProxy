using System.Text.Json;

using Shouldly;

namespace NestProxy.Rtsp.Tests;

/// <summary>
/// Provides extension methods for asserting test outcomes.
/// </summary>
public static class TestAssertionExtensions
{
    /// <summary>
    /// Asserts that the elements in the actual sequence contain all the expected values in the specified order.
    /// </summary>
    /// <remarks>
    /// The method checks that each element in the expected array appears in the actual sequence and
    /// that their order matches. The actual sequence may contain additional elements not present in expected, but all
    /// expected elements must appear in the specified order. Throws an exception if the order is incorrect or if any
    /// expected element is missing.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the sequences being compared.</typeparam>
    /// <param name="actual">The sequence to be examined for the presence and order of the expected elements.</param>
    /// <param name="expected">An array of elements that are expected to appear in order within the actual sequence.</param>
    public static void ShouldContainInOrder<T>(this IEnumerable<T> actual, params T[] expected)
    {
        var list = actual.ToList();
        var lastIndex = -1;

        foreach (var item in expected)
        {
            var index = list.IndexOf(item);
            index.ShouldBeGreaterThan(lastIndex, $"Expected '{item}' to appear after previous items");
            lastIndex = index;
        }
    }

    /// <summary>
    /// Asserts that all elements in the collection are equivalent to the expected value.
    /// </summary>
    /// <remarks>
    /// This method is typically used in unit tests to verify that every item in a collection matches
    /// the expected value according to the equivalence criteria defined by ShouldBeEquivalentTo. If any element is not
    /// equivalent, the assertion fails for that element.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the collection to compare.</typeparam>
    /// <param name="actual">The collection of elements to be compared against the expected value.</param>
    /// <param name="expected">The value to which each element in the collection is compared for equivalence.</param>
    public static void ShouldAllBeEquivalentTo<T>(this IEnumerable<T> actual, T expected)
    {
        foreach (var item in actual)
        {
            item.ShouldBeEquivalentTo(expected);
        }
    }

    /// <summary>
    /// Asserts that the actual object is structurally equivalent to the expected object, using a deep comparison of
    /// their serialized representations.
    /// </summary>
    /// <remarks>
    /// This method performs a deep comparison by serializing both objects to JSON and comparing the
    /// resulting strings. Differences in serialization (such as property order or ignored properties) may affect the
    /// outcome. Use this method for simple structural equivalence checks; for more advanced scenarios, consider using a
    /// dedicated object comparison library.
    /// </remarks>
    /// <typeparam name="T">The type of the objects to compare.</typeparam>
    /// <param name="actual">The object being tested for equivalence.</param>
    /// <param name="expected">The object to compare against for equivalence.</param>
    public static void ShouldBeEquivalentTo<T>(this T actual, T expected)
    {
        // Naive deep compare using JSON.
        var a = JsonSerializer.Serialize(actual);
        var b = JsonSerializer.Serialize(expected);

        a.ShouldBe(b);
    }
}
