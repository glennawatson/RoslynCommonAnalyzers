// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the string and text rules (PSH12xx). These target
/// throwaway string allocations around comparisons, searches, and builders.
/// </summary>
internal static class StringRules
{
    /// <summary>PSH1200 — comparisons should not allocate case-converted copies.</summary>
    public static readonly DiagnosticDescriptor AvoidCaseConversionComparison = Create(
        "PSH1200",
        "Compare strings without allocating case-converted copies",
        "Use a case-insensitive StringComparison overload instead of '{0}'",
        "Calling ToLower or ToUpper to compare strings allocates a converted copy per operand on every comparison; the overloads that take a StringComparison compare in place without allocating.");

    /// <summary>PSH1201 — single-character string arguments should use the char overload.</summary>
    public static readonly DiagnosticDescriptor UseCharOverload = Create(
        "PSH1201",
        "Use the char overload for single-character strings",
        "Pass the character {0} instead of a single-character string",
        "String search overloads pay full comparison setup for a one-character argument; the char overloads scan directly. Only ordinal-equivalent call shapes are reported so behavior is unchanged.");

    /// <summary>PSH1202 — StringBuilder should append a one-character literal as a char.</summary>
    public static readonly DiagnosticDescriptor StringBuilderAppendChar = Create(
        "PSH1202",
        "Append characters as char, not single-character strings",
        "Use the char overload of '{0}'",
        "StringBuilder.Append and Insert with a one-character string literal go through string length and copy machinery; the char overloads write the character directly into the buffer.");

    /// <summary>PSH1203 — StringBuilder can do the appended formatting work itself.</summary>
    public static readonly DiagnosticDescriptor StringBuilderInnerAllocation = Create(
        "PSH1203",
        "Let StringBuilder do the formatting work",
        "Use '{0}' instead of allocating an intermediate string",
        "Appending string.Format results, ToString results, or substrings creates a throwaway string per call; AppendFormat, typed Append overloads, and Append(string, int, int) write directly.");

    /// <summary>PSH1204 — emptiness is a length check, not a string comparison.</summary>
    public static readonly DiagnosticDescriptor EmptyStringComparison = Create(
        "PSH1204",
        "Test for empty strings by length",
        "Use a length check instead of comparing to an empty string",
        "Comparing against \"\" or string.Empty runs the string-equality path; checking Length against zero is a field read. The fix uses a null-safe length pattern.");

    /// <summary>PSH1205 — an interpolated string that does no interpolation still pays for it.</summary>
    public static readonly DiagnosticDescriptor RedundantInterpolatedString = Create(
        "PSH1205",
        "Remove interpolation that does no work",
        "Replace this interpolated string with {0}",
        "An interpolated string that only wraps a single string value, or contains no holes at all, still runs the interpolation machinery and allocates; the plain value or literal is free.");

    /// <summary>PSH1206 — string concatenation inside a loop is quadratic.</summary>
    public static readonly DiagnosticDescriptor StringConcatenationInLoop = Create(
        "PSH1206",
        "Do not build strings by concatenation in loops",
        "Accumulating '{0}' by concatenation copies the whole value every iteration; use a StringBuilder",
        "Appending to a string inside a loop copies the entire accumulated value on every iteration, making the loop quadratic; a StringBuilder appends in place.");

    /// <summary>PSH1207 — culture-sensitive string searches should specify a StringComparison.</summary>
    public static readonly DiagnosticDescriptor SpecifyStringComparison = Create(
        "PSH1207",
        "Specify StringComparison for culture-sensitive string operations",
        "Specify a StringComparison for '{0}' to avoid the culture-sensitive default",
        "StartsWith, EndsWith, IndexOf, LastIndexOf, and string.Compare with only string arguments compare using the current culture; a StringComparison argument selects the cheaper Ordinal.");

    /// <summary>PSH1208 — a constant string encoded at run time should be a u8 literal.</summary>
    public static readonly DiagnosticDescriptor UseUtf8Literal = Create(
        "PSH1208",
        "Encode constant strings with u8 literals",
        "Use a u8 string literal instead of encoding this constant at run time",
        UseUtf8LiteralDescription);

    /// <summary>PSH1209 — a copied-and-mutated char array should be a string.Create call.</summary>
    public static readonly DiagnosticDescriptor UseStringCreate = Create(
        "PSH1209",
        "Build transformed strings with string.Create",
        "Use string.Create instead of mutating a copied char array into a new string",
        UseStringCreateDescription);

    /// <summary>PSH1210 — UTF-8 bytes compared against a constant should not be decoded first.</summary>
    public static readonly DiagnosticDescriptor UseUtf8SequenceEqual = Create(
        "PSH1210",
        "Compare UTF-8 bytes without decoding them",
        "Compare the bytes with SequenceEqual against a u8 literal instead of decoding with '{0}'",
        UseUtf8SequenceEqualDescription);

    /// <summary>PSH1211 — a ToString result fed to an API that can take the value is a throwaway string.</summary>
    public static readonly DiagnosticDescriptor RemoveIntermediateToString = Create(
        "PSH1211",
        "Pass values directly instead of ToString results",
        "Pass the value directly instead of calling ToString",
        RemoveIntermediateToStringDescription);

    /// <summary>PSH1212 — a Substring feeding a span-capable call should slice with AsSpan.</summary>
    public static readonly DiagnosticDescriptor UseAsSpanOverSubstring = Create(
        "PSH1212",
        "Slice with AsSpan when the call accepts a span",
        "Use AsSpan instead of Substring; '{0}' accepts a span here",
        UseAsSpanOverSubstringDescription);

    /// <summary>PSH1213 — repeated probes of a constant character set should go through SearchValues.</summary>
    public static readonly DiagnosticDescriptor UseSearchValues = Create(
        "PSH1213",
        "Probe repeated character sets through SearchValues",
        "Hoist this constant set into a cached SearchValues and search with that",
        UseSearchValuesDescription);

    /// <summary>PSH1214 — a StringBuilder append concatenates its argument first.</summary>
    public static readonly DiagnosticDescriptor SplitConcatenatedAppend = Create(
        "PSH1214",
        "Append the parts, not a concatenated whole",
        "Split this concatenation into separate Append calls",
        SplitConcatenatedAppendDescription);

    /// <summary>PSH1215 — string.Join is called with an empty separator.</summary>
    public static readonly DiagnosticDescriptor UseConcatOverEmptyJoin = Create(
        "PSH1215",
        "Concatenate when there is no separator",
        "Use string.Concat instead of string.Join with an empty separator",
        UseConcatOverEmptyJoinDescription);

    /// <summary>PSH1216 — an ordering comparison result is only tested for equality.</summary>
    public static readonly DiagnosticDescriptor UseEqualsOverCompare = Create(
        "PSH1216",
        "Ask for equality, not ordering",
        "Use string.Equals instead of comparing '{0}' to zero",
        UseEqualsOverCompareDescription);

    /// <summary>PSH1217 — a sequence is copied to an array only to be read straight back.</summary>
    public static readonly DiagnosticDescriptor RedundantSequenceCopy = Create(
        "PSH1217",
        "Do not copy a sequence to an array just to read it",
        "'{0}' allocates a copy that '{1}' does not need; pass the {2} directly",
        RedundantSequenceCopyDescription);

    /// <summary>PSH1218 — a substring is allocated only to search it.</summary>
    public static readonly DiagnosticDescriptor SearchWithStartIndex = Create(
        "PSH1218",
        "Slice with AsSpan instead of allocating a substring to search it",
        "'{0}' allocates a substring only to search it; slice with 'AsSpan' so '{1}' searches in place",
        SearchWithStartIndexDescription);

    /// <summary>The PSH1208 rule description.</summary>
    private const string UseUtf8LiteralDescription =
        "Encoding.UTF8.GetBytes on a constant string re-encodes and heap-allocates the same bytes on every call; a u8 literal (C# 11+) "
        + "is encoded once at compile time and read straight from the assembly's data section.";

    /// <summary>The PSH1209 rule description.</summary>
    private const string UseStringCreateDescription =
        "The ToCharArray copy, the in-place mutation, and the new string constructor allocate two throwaway buffers to produce one "
        + "result; string.Create allocates the final string once and exposes its buffer as a writable span. Suggested only where the "
        + "API exists.";

    /// <summary>The PSH1210 rule description.</summary>
    private const string UseUtf8SequenceEqualDescription =
        "Decoding bytes with Encoding.GetString allocates a string only to compare it against a constant and throw it away; "
        + "SequenceEqual against a u8 literal (C# 11+) compares the raw bytes and allocates nothing.";

    /// <summary>The PSH1212 rule description.</summary>
    private const string UseAsSpanOverSubstringDescription =
        "Substring allocates a new string for the slice; when the invoked method has an overload accepting ReadOnlySpan<char> in the "
        + "same position, AsSpan produces the slice with no allocation. Reported only when that overload exists and AsSpan resolves at "
        + "the call site, so the fix always compiles.";

    /// <summary>The PSH1213 rule description.</summary>
    private const string UseSearchValuesDescription =
        "IndexOfAny and ContainsAny against an inline constant array rescan the set linearly on every call and can allocate the array "
        + "each time; a static readonly SearchValues<char> (.NET 8+) precomputes the membership table once and the overloads that take "
        + "it probe in constant time per character. Suggested only where the API exists.";

    /// <summary>The PSH1211 rule description.</summary>
    private const string RemoveIntermediateToStringDescription =
        "Calling ToString to feed an API that can take the value directly allocates an intermediate string; overloads that accept the "
        + "value, and interpolated string holes, format without the throwaway copy — value types with span formatting write straight "
        + "into the destination buffer.";

    /// <summary>The PSH1214 rule description.</summary>
    private const string SplitConcatenatedAppendDescription =
        "Appending 'a + b' builds an intermediate string only to copy its characters into the builder; chaining Append(a).Append(b) "
        + "writes each part straight into the buffer. The compiler already folds all-constant concatenations, so only concatenations "
        + "with a non-constant operand are reported.";

    /// <summary>The PSH1215 rule description.</summary>
    private const string UseConcatOverEmptyJoinDescription =
        "string.Join with an empty separator still runs the separator-insertion bookkeeping between every element; string.Concat "
        + "copies the pieces straight through.";

    /// <summary>The PSH1216 rule description.</summary>
    private const string UseEqualsOverCompareDescription =
        "string.Compare computes full ordering information that an equality test throws away, and cannot bail out early on length "
        + "mismatches the way string.Equals can. The fix preserves the comparison's culture semantics — the two-argument Compare "
        + "defaults to the current culture, not ordinal.";

    /// <summary>The PSH1217 rule description.</summary>
    private const string RedundantSequenceCopyDescription =
        "'string.ToCharArray()' and 'ReadOnlySpan<T>.ToArray()' allocate and copy. When the result is handed straight to something that "
        + "already accepts the original — a 'foreach', an indexer, a 'Length' read, or any parameter that takes a 'ReadOnlySpan<char>' or a "
        + "'string' — the copy is pure waste. Pass the string or the span itself. A copy that is retained, mutated, or handed to an API that "
        + "genuinely needs an array is left alone.";

    /// <summary>The PSH1218 rule description.</summary>
    private const string SearchWithStartIndexDescription =
        "'text.Substring(i).IndexOf(value)' copies the whole tail of the string before it looks at a single character. 'AsSpan(i)' slices "
        + "the same characters in place and allocates nothing, and 'IndexOf', 'LastIndexOf', 'StartsWith', 'EndsWith' and 'Contains' all "
        + "search a span directly. The result needs no adjustment: a span search reports its hit relative to the span, which is the basis the "
        + "substring already had, and still answers -1 on a miss — so the value is the same whether it is used as a boolean or as an index. "
        + "(The 'IndexOf(value, i)' string overload is NOT equivalent: it reports relative to the original string. 'LastIndexOf(value, i)' is "
        + "worse still, searching backward from 'i'.) A comparison is only rewritten when it is ordinal-equivalent, so a culture-sensitive "
        + "search is never silently made ordinal, and the rewritten call is bound before it is offered, so an overload the target framework "
        + "lacks is never suggested. A substring used for anything besides the search is not reported.";

    /// <summary>Creates a Warning-severity Strings descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Strings", description);
}
