// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Diagnostic descriptors for collection-expression rules (SST21xx).</summary>
internal static class CollectionExpressionRules
{
    /// <summary>SST2100 — an empty collection creation can use <c>[]</c>.</summary>
    public static readonly DiagnosticDescriptor UseEmptyCollectionExpression = Create(
        "SST2100",
        "Use an empty collection expression",
        "Replace this empty collection creation with '[]'",
        "An empty array or standard generic collection is created with the C# 12 collection expression '[]'.");

    /// <summary>SST2101 — an explicit collection creation can use <c>[...]</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseExplicitCollectionExpression = CreateOptIn(
        "SST2101",
        "Use a collection expression",
        "Replace this collection creation with a collection expression",
        "A standard array or generic collection initializer is written as a C# 12 collection expression. Off by default because target-type inference can affect overload resolution.");

    /// <summary>SST2102 — a stackalloc initializer can use <c>[...]</c>.</summary>
    public static readonly DiagnosticDescriptor UseCollectionExpressionForStackalloc = Create(
        "SST2102",
        "Let the span target carry stack allocation",
        "Replace this stackalloc initializer with a collection expression",
        "A stackalloc initializer assigned to a span target is written as a C# 12 collection expression when the referenced framework supports the conversion.");

    /// <summary>SST2103 — a collection-builder <c>Create</c> call can use <c>[...]</c>.</summary>
    public static readonly DiagnosticDescriptor UseCollectionExpressionForCreate = Create(
        "SST2103",
        "Use a collection expression for this factory call",
        "Replace this collection factory call with a collection expression",
        "A collection-builder Create or CreateRange call is written as a collection expression so the elements are visible at the assignment site.");

    /// <summary>SST2104 — a short builder sequence can use <c>[...]</c>.</summary>
    public static readonly DiagnosticDescriptor UseCollectionExpressionForBuilder = Create(
        "SST2104",
        "Return builder contents directly",
        "Replace this builder sequence with a collection expression",
        "A local builder that is only populated with Add calls and immediately converted is replaced by a collection expression.");

    /// <summary>SST2105 — a fluent array conversion can use <c>[...]</c>.</summary>
    public static readonly DiagnosticDescriptor UseCollectionExpressionForFluent = Create(
        "SST2105",
        "Use the target collection expression directly",
        "Replace this fluent array conversion with a collection expression",
        "A literal array immediately converted with ToArray or ToList is written as a collection expression at the target site.");

    /// <summary>Creates an enabled collection-expression descriptor.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The title.</param>
    /// <param name="messageFormat">The message.</param>
    /// <param name="description">The description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "CollectionExpressions", description);

    /// <summary>Creates an opt-in collection-expression descriptor.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The title.</param>
    /// <param name="messageFormat">The message.</param>
    /// <param name="description">The description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "CollectionExpressions", description);
}
