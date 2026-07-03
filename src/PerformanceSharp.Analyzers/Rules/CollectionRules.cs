// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the collection and enumeration rules (PSH11xx).
/// These target enumeration work that a collection can answer directly: redundant
/// iterator layers, repeated hash lookups, and LINQ calls on paths that need
/// explicit iteration.
/// </summary>
internal static class CollectionRules
{
    /// <summary>PSH1100 — hot-path code should avoid LINQ extension methods (opt-in).</summary>
    public static readonly DiagnosticDescriptor AvoidLinqOnHotPath = CreateOptIn(
        "PSH1100",
        "Avoid LINQ calls on hot paths",
        "Replace this LINQ call with explicit iteration on performance-sensitive paths",
        "Hot-path code avoids System.Linq.Enumerable calls where iterator allocation, delegate invocation, and abstraction overhead can dominate.");

    /// <summary>PSH1101 — a LINQ terminal call can carry the preceding predicate directly.</summary>
    public static readonly DiagnosticDescriptor CollapseLinqWhereTerminal = Create(
        "PSH1101",
        "Carry LINQ predicates at the terminal call",
        "Move this Where predicate into the terminal LINQ call",
        "A Where call immediately followed by a terminal LINQ call that accepts a predicate keeps the predicate in the terminal call and removes an iterator layer.");

    /// <summary>PSH1102 — a LINQ type filter and cast can use one typed filter call.</summary>
    public static readonly DiagnosticDescriptor CollapseLinqTypeFilter = Create(
        "PSH1102",
        "Filter LINQ values by type once",
        "Replace the type check and cast chain with one typed filter",
        "A LINQ Where type check followed by Cast is written as a single typed filter call so the sequence performs one pass over each element.");

    /// <summary>PSH1103 — a collection with an O(1) count should not be counted by enumeration.</summary>
    public static readonly DiagnosticDescriptor UseCountProperty = Create(
        "PSH1103",
        "Prefer the collection's own count over enumerating",
        "Use '{0}' instead of this Enumerable call",
        "Calling Enumerable.Count() or Any() on a collection with a Count or Length property allocates an enumerator and may scan; a direct property read answers in constant time.");

    /// <summary>PSH1104 — a <c>ContainsKey</c> guard around an indexer read should be <c>TryGetValue</c>.</summary>
    public static readonly DiagnosticDescriptor UseTryGetValue = Create(
        "PSH1104",
        "Use TryGetValue instead of ContainsKey followed by an indexer read",
        "Combine this ContainsKey check and indexer read into TryGetValue",
        "Guarding a dictionary indexer read with ContainsKey performs the key hash lookup twice; TryGetValue answers both questions with a single lookup.");

    /// <summary>PSH1105 — a membership guard around a mutating call repeats the lookup.</summary>
    public static readonly DiagnosticDescriptor AvoidDoubleLookup = Create(
        "PSH1105",
        "Avoid double lookups on dictionaries and sets",
        "Remove this '{0}' guard; '{1}' already reports whether it acted",
        "Guarding Add or Remove with Contains or ContainsKey repeats the hash lookup; the mutating call already returns whether it changed the collection.");

    /// <summary>PSH1106 — list-like receivers should be indexed instead of enumerated for element access.</summary>
    public static readonly DiagnosticDescriptor UseIndexerForElementAccess = Create(
        "PSH1106",
        "Index collections directly instead of using LINQ element access",
        "Use the indexer instead of '{0}'",
        "First(), Last(), and ElementAt() on a list-like receiver allocate an enumerator and may scan the sequence; the indexer reads the element directly.");

    /// <summary>PSH1107 — a filter applied after a sort pays to order discarded elements.</summary>
    public static readonly DiagnosticDescriptor FilterBeforeSort = Create(
        "PSH1107",
        "Filter sequences before sorting them",
        "Move this Where before the OrderBy so discarded elements are not sorted",
        "Sorting costs O(n log n); filtering after an OrderBy pays to order elements that are immediately thrown away. Filtering first sorts only the survivors.");

    /// <summary>PSH1108 — a second <c>OrderBy</c> discards the first sort instead of refining it.</summary>
    public static readonly DiagnosticDescriptor UseThenBy = Create(
        "PSH1108",
        "Chain secondary sorts with ThenBy",
        "Use '{0}' so the preceding sort's work is kept",
        "An OrderBy applied to an already ordered sequence re-sorts everything and discards the previous ordering; ThenBy and ThenByDescending refine it instead.");

    /// <summary>PSH1109 — consecutive <c>Where</c> calls stack iterator layers.</summary>
    public static readonly DiagnosticDescriptor MergeConsecutiveWhere = Create(
        "PSH1109",
        "Merge consecutive Where calls",
        "Combine these Where predicates into one call",
        "Each Where adds an iterator object and a delegate invocation per element; consecutive filters combine into a single predicate with &&.");

    /// <summary>PSH1110 — a collection's own predicate method beats the LINQ extension.</summary>
    public static readonly DiagnosticDescriptor UseCollectionNativePredicate = Create(
        "PSH1110",
        "Use the collection's own predicate methods over LINQ",
        "Use '{0}' instead of this Enumerable call",
        "List<T> and arrays provide Find, Exists, and TrueForAll that run without enumerator allocation or interface dispatch; the FirstOrDefault, Any, and All extensions pay for both.");

    /// <summary>PSH1111 — an equality predicate passed to <c>Any</c> is a membership test.</summary>
    public static readonly DiagnosticDescriptor UseContainsForMembership = Create(
        "PSH1111",
        "Use Contains for membership tests",
        "Use Contains instead of this equality predicate",
        "Any with an equality-only predicate allocates an enumerator and calls a delegate per element; Contains scans directly and is O(1) on hash sets.");

    /// <summary>Creates a Warning-severity Collections descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Collections",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a Collections descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Collections",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
