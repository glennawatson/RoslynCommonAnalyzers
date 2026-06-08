// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the modernization rules (SST20xx). Each suggests a
/// modern runtime throw-helper in place of a hand-written argument guard, and each
/// is gated on the helper actually existing in the referenced framework, so the
/// rule lights up only where the replacement compiles.
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

    /// <summary>Creates a Warning-severity Modernization descriptor whose help link points at the rule's docs page.</summary>
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
            "Modernization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>Creates a Modernization descriptor that is disabled by default (opt-in via .editorconfig).</summary>
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
            "Modernization",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
