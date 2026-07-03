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

    /// <summary>Creates a Warning-severity Strings descriptor whose help link points at the rule's docs page.</summary>
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
            "Strings",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
