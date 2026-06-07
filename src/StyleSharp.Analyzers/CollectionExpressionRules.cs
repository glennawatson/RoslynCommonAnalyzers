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

    /// <summary>Creates an enabled collection-expression descriptor.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The title.</param>
    /// <param name="messageFormat">The message.</param>
    /// <param name="description">The description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "CollectionExpressions",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates an opt-in collection-expression descriptor.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The title.</param>
    /// <param name="messageFormat">The message.</param>
    /// <param name="description">The description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "CollectionExpressions",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
