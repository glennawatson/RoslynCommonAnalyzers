// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Diagnostic descriptors for the design rules (SST23xx). These are about the shape of a type's
/// surface — the contracts it signs up to (<c>IDisposable</c>, <c>IEquatable&lt;T&gt;</c>), the
/// conventions its operators and events follow, and what its members hand out.
/// </summary>
internal static partial class DesignRules
{
    /// <summary>Creates a Warning-severity Design descriptor whose help link points at the rule's docs page.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, string description) =>
        DescriptorFactory.Create(id, title, messageFormat, "Design", description);

    /// <summary>
    /// Creates an enabled-by-default Info-severity Design descriptor — a design nudge where the code still compiles
    /// and runs correctly, so a build-breaking Warning would be too strong.
    /// </summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateInfo(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Design",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");

    /// <summary>
    /// Creates a disabled-by-default Warning-severity Design descriptor — an opinionated shape whose call is a
    /// matter of house style rather than a defect, so it stays opt-in through <c>.editorconfig</c> rather than
    /// on by default.
    /// </summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="title">The rule title.</param>
    /// <param name="messageFormat">The message format.</param>
    /// <param name="description">The rule description.</param>
    /// <returns>The descriptor.</returns>
    private static DiagnosticDescriptor CreateDisabled(string id, string title, string messageFormat, string description) =>
        new(
            id,
            title,
            messageFormat,
            "Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false,
            description: description,
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
