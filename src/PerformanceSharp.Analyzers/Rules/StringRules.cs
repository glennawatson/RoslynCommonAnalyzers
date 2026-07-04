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

    /// <summary>Creates a Warning-severity Strings descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Strings", description);
}
