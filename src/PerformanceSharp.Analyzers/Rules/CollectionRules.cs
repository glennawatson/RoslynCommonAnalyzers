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

    /// <summary>PSH1112 — a collection filled right after creation should be seeded through its constructor.</summary>
    public static readonly DiagnosticDescriptor SeedCollectionFromSource = Create(
        "PSH1112",
        "Seed the collection through its constructor",
        "Pass the source to the '{0}' constructor instead of calling '{1}' on an empty instance",
        SeedCollectionFromSourceDescription);

    /// <summary>PSH1113 — an identity key selector asks for the natural sort.</summary>
    public static readonly DiagnosticDescriptor UseNaturalOrder = Create(
        "PSH1113",
        "Sort naturally instead of ordering by the element itself",
        "Use '{0}' instead of '{1}' with an identity selector",
        UseNaturalOrderDescription);

    /// <summary>PSH1114 — a read-only static lookup table can be frozen. Opt-in.</summary>
    public static readonly DiagnosticDescriptor FreezeStaticLookups = CreateOptIn(
        "PSH1114",
        "Freeze static lookup collections that are never mutated",
        "Use a Frozen{0} for '{1}'; it is built once and only read",
        FreezeStaticLookupsDescription);

    /// <summary>PSH1115 — insert-if-absent written as a guard plus a write probes the dictionary twice.</summary>
    public static readonly DiagnosticDescriptor SingleProbeInsert = Create(
        "PSH1115",
        "Insert-if-absent should probe the dictionary once",
        "Use '{0}' so the key is hashed once instead of twice",
        SingleProbeInsertDescription);

    /// <summary>PSH1116 — a string materialized only to probe a lookup should be a span probe.</summary>
    public static readonly DiagnosticDescriptor UseAlternateLookup = Create(
        "PSH1116",
        "Probe string-keyed collections with a span through GetAlternateLookup",
        "Use GetAlternateLookup<ReadOnlySpan<char>> instead of materializing the key with '{0}'",
        UseAlternateLookupDescription);

    /// <summary>PSH1117 — emptiness should be asked directly where IsEmpty exists.</summary>
    public static readonly DiagnosticDescriptor UseIsEmpty = Create(
        "PSH1117",
        "Ask the collection whether it is empty",
        "Use IsEmpty instead of comparing '{0}' to zero",
        UseIsEmptyDescription);

    /// <summary>PSH1118 — a sequence is sorted just to take one extreme element.</summary>
    public static readonly DiagnosticDescriptor TakeExtremeWithoutSorting = Create(
        "PSH1118",
        "Take the extreme element without sorting",
        "Use '{0}' instead of ordering the sequence to take one element",
        TakeExtremeWithoutSortingDescription);

    /// <summary>PSH1119 — a full count is computed only to compare it against zero.</summary>
    public static readonly DiagnosticDescriptor UseAnyOverCount = Create(
        "PSH1119",
        "Check for elements without counting them all",
        "Use '{0}' instead of counting the whole sequence",
        UseAnyOverCountDescription);

    /// <summary>PSH1120 — a sequence is copied into a collection just to be enumerated once.</summary>
    public static readonly DiagnosticDescriptor DoNotMaterializeToEnumerate = Create(
        "PSH1120",
        "Do not materialize a sequence just to enumerate it",
        "Enumerate the source directly instead of copying it with '{0}'",
        DoNotMaterializeToEnumerateDescription);

    /// <summary>PSH1122 — a sorted set's extreme element is fetched through LINQ.</summary>
    public static readonly DiagnosticDescriptor UseSortedSetExtremeProperty = Create(
        "PSH1122",
        "Use a sorted set's Min and Max properties",
        "'{0}' scans the whole set; '{1}' reads the {2} element directly",
        UseSortedSetExtremePropertyDescription);

    /// <summary>The PSH1117 rule description.</summary>
    private const string UseIsEmptyDescription =
        "Comparing Count or Length to zero answers emptiness indirectly — on some collections counting is O(n) or synchronizes, while "
        + "IsEmpty is a cheap dedicated check (concurrent collections, immutable collections, spans, and memory). Reported only when "
        + "the receiver exposes a bool IsEmpty property.";

    /// <summary>The PSH1112 rule description.</summary>
    private const string SeedCollectionFromSourceDescription =
        "Creating an empty collection and immediately bulk-adding a source grows the backing store through the default resize schedule; "
        + "the seeding constructor sizes the store once from the source's count and copies in a single pass.";

    /// <summary>The PSH1113 rule description.</summary>
    private const string UseNaturalOrderDescription =
        "OrderBy(x => x) routes every comparison through a key-selector delegate to return the element unchanged; Order and OrderDescending "
        + "(.NET 7+) compare elements directly. Suggested only where the API exists.";

    /// <summary>The PSH1114 rule description.</summary>
    private const string FreezeStaticLookupsDescription =
        "A private static readonly dictionary or set that is initialized once and never mutated can become a FrozenDictionary or FrozenSet "
        + "(.NET 8+), trading construction cost for faster lookups. Freezing is not free — construction is markedly slower and only "
        + "read-heavy tables win — so the rule is opt-in. Suggested only where the API exists.";

    /// <summary>The PSH1115 rule description.</summary>
    private const string SingleProbeInsertDescription =
        "Guarding an indexer write with ContainsKey, or storing after a failed TryGetValue, hashes and probes the key twice; TryAdd "
        + "inserts and answers in one probe, and CollectionsMarshal.GetValueRefOrAddDefault exposes the value slot so a missing entry "
        + "can be created and stored with a single lookup. Each API is suggested only where it exists.";

    /// <summary>The PSH1116 rule description.</summary>
    private const string UseAlternateLookupDescription =
        "Allocating a string just to look it up throws the copy away after hashing; GetAlternateLookup (.NET 9+) probes string-keyed "
        + "dictionaries and sets with a ReadOnlySpan<char> key and allocates nothing. The collection's comparer must support alternate "
        + "lookups — the ordinal defaults do. Suggested only where the API exists.";

    /// <summary>The PSH1118 rule description.</summary>
    private const string TakeExtremeWithoutSortingDescription =
        "OrderBy followed by First or Last sorts the entire sequence — an O(n log n) pass over an allocated buffer — to keep a single "
        + "element; Min, Max, MinBy, and MaxBy scan once with no buffer. MinBy and MaxBy are suggested only where the API exists, and "
        + "the docs page lists the exact mapping including empty-sequence behavior.";

    /// <summary>The PSH1119 rule description.</summary>
    private const string UseAnyOverCountDescription =
        "Enumerable.Count() walks the whole sequence to produce a number that the comparison immediately collapses to a yes or no; "
        + "Any() stops at the first element. The parameterless form is reported only for sources without an O(1) Count or Length "
        + "property — those are PSH1103's territory; the predicate form counts every match, so it is reported on any receiver.";

    /// <summary>The PSH1120 rule description.</summary>
    private const string DoNotMaterializeToEnumerateDescription =
        "ToList and ToArray copy every element into a new collection that is discarded when the loop ends; enumerating the source "
        + "directly skips the buffer and its growth copies. Not reported when the loop body mentions the source again, where the copy "
        + "may be guarding against modification during enumeration.";

    /// <summary>The PSH1122 rule description.</summary>
    private const string UseSortedSetExtremePropertyDescription =
        "'SortedSet<T>' and 'ImmutableSortedSet<T>' keep their elements in order, so the smallest and largest are already at the ends: the "
        + "'Min' and 'Max' properties return them in constant time. The 'Enumerable.Min' and 'Enumerable.Max' extensions know nothing about the "
        + "receiver and walk every element to rediscover what the set already knows. Only a receiver whose static type is a sorted set is "
        + "reported, and only for the parameterless extensions — one that takes a selector or a comparer is asking a different question.";

    /// <summary>Creates a Warning-severity Collections descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Collections", description);

    /// <summary>Creates a Collections descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Collections", description);
}
