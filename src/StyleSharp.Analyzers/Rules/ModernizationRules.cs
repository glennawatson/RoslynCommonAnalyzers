// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the modernization rules (SST20xx). These point hand-written
/// code at a clearer modern equivalent — a runtime throw-helper in place of an argument
/// guard, or a pattern-matching form in place of an <c>as</c>/<c>is</c> idiom. Throw-helper
/// rules are gated on the helper existing in the referenced framework, so they light up only
/// where the replacement compiles.
/// </summary>
internal static class ModernizationRules
{
    /// <summary>SST2000 — a null check + throw should use <c>ArgumentNullException.ThrowIfNull</c>.</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNull = Create(
        "SST2000",
        "Use ArgumentNullException.ThrowIfNull",
        "Replace the null check with 'ArgumentNullException.ThrowIfNull({0})'",
        "A null check that throws ArgumentNullException is replaced by the ArgumentNullException.ThrowIfNull guard helper (.NET 6+).");

    /// <summary>SST2001 — an empty-string check + throw should use <c>ArgumentException.ThrowIfNullOrEmpty</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNullOrEmpty = CreateOptIn(
        "SST2001",
        "Use ArgumentException.ThrowIfNullOrEmpty",
        "Replace the check with 'ArgumentException.ThrowIfNullOrEmpty({0})'",
        "A string.IsNullOrEmpty check that throws is replaced by ArgumentException.ThrowIfNullOrEmpty (.NET 7+). Off by default — it can change the thrown message and exception type.");

    /// <summary>SST2002 — a whitespace check + throw should use <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> (opt-in).</summary>
    public static readonly DiagnosticDescriptor UseThrowIfNullOrWhiteSpace = CreateOptIn(
        "SST2002",
        "Use ArgumentException.ThrowIfNullOrWhiteSpace",
        "Replace the check with 'ArgumentException.ThrowIfNullOrWhiteSpace({0})'",
        "A string.IsNullOrWhiteSpace check that throws is replaced by ArgumentException.ThrowIfNullOrWhiteSpace (.NET 8+). Off by default — it can change the thrown message and exception type.");

    /// <summary>SST2003 — a disposed check should use <c>ObjectDisposedException.ThrowIf</c>.</summary>
    public static readonly DiagnosticDescriptor UseObjectDisposedThrowIf = Create(
        "SST2003",
        "Use ObjectDisposedException.ThrowIf",
        "Replace the disposed check with 'ObjectDisposedException.ThrowIf({0}, this)'",
        "A standard disposed check is replaced by ObjectDisposedException.ThrowIf (.NET 8+).");

    /// <summary>SST2004 — a range check should use an <c>ArgumentOutOfRangeException.ThrowIf...</c> helper.</summary>
    public static readonly DiagnosticDescriptor UseArgumentOutOfRangeThrowIf = Create(
        "SST2004",
        "Use ArgumentOutOfRangeException range helpers",
        "Replace the range check with 'ArgumentOutOfRangeException.{0}'",
        "A simple range check is replaced by the matching ArgumentOutOfRangeException.ThrowIf helper (.NET 8+).");

    /// <summary>SST2005 — an <c>as</c> cast compared to <c>null</c> should use the <c>is</c> type pattern.</summary>
    public static readonly DiagnosticDescriptor UseIsPatternOverAsNullCheck = Create(
        "SST2005",
        "Use the 'is' type pattern instead of comparing an 'as' cast to null",
        "Use '{0}' instead of comparing an 'as' cast to null",
        "Casting with 'as' and then comparing to null ('x as T != null') restates a type test that 'x is T' (or 'x is not T') expresses directly, without the throwaway local.");

    /// <summary>SST2006 — a negated type test (<c>!(x is T)</c>) should use the <c>is not</c> pattern.</summary>
    public static readonly DiagnosticDescriptor UseNegatedIsPattern = Create(
        "SST2006",
        "Use the 'is not' pattern instead of negating an 'is' check",
        "Use 'is not' instead of negating the 'is' check",
        "A type test negated with '!' reads more directly as the 'is not' pattern: '!(x is T)' becomes 'x is not T'.");

    /// <summary>SST2007 — an <c>is</c> check followed by a cast should use a declaration pattern.</summary>
    public static readonly DiagnosticDescriptor UseDeclarationPatternOverIsCheckAndCast = Create(
        "SST2007",
        "Declare the checked type in the pattern",
        "Declare '{0}' in the 'is' pattern instead of casting after the check",
        "An 'is' type check followed by a local cast of the same value reads more directly as a declaration pattern: 'x is T t'.");

    /// <summary>SST2008 — a negated pattern test should use an <c>is not</c> pattern.</summary>
    public static readonly DiagnosticDescriptor UseIsNotPattern = Create(
        "SST2008",
        "Use an is-not pattern instead of negating a pattern",
        "Use 'is not' instead of negating this pattern test",
        "A pattern test negated with '!' reads more directly as 'is not', and avoids wrapping the whole pattern expression in an extra grouping expression.");

    /// <summary>Creates a Warning-severity Modernization descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Modernization", description);

    /// <summary>Creates a Modernization descriptor that is disabled by default (opt-in via .editorconfig).</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateOptIn(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.CreateOptIn(id, title, messageFormat, "Modernization", description);
}
