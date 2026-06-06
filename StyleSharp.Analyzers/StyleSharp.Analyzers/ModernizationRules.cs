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
}
